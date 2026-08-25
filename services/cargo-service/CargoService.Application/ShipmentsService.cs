using System.Security.Cryptography;
using System.Text.Json;
using CargoService.Application.Interfaces;
using CargoService.Application.Models;
using CargoService.Contracts.Events.V1;
using CargoService.Contracts.Messaging;
using CargoService.Domain.Entities;
using CargoService.Domain.Enums;

namespace CargoService.Application;

public class ShipmentsService(
    IShipmentsRepository shipmentsRepository,
    IOutboxWriter outboxWriter) : IShipmentsService
{
    private const string ServiceName = "cargo-service";

    /// <summary>
    /// Порог уверенности модели, ниже которого её вердикт не считается расхождением. Величина
    /// бизнесовая, а не техническая: её стоит подкрутить, когда появится статистика ложных
    /// срабатываний ai-inspection-service (Фаза 8).
    /// </summary>
    private const double MinDiscrepancyConfidence = 0.7;

    // Тот же алфавит без 0/O/1/I, что у номера заявки в orders-service: трек-номер клиенты
    // диктуют по телефону и вбивают руками, визуально неоднозначные символы здесь дороже всего.
    private const string TrackingAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int TrackingSuffixLength = 10;
    private const string TrackingPrefix = "CS-";

    public async Task<Shipment?> CreateFromConfirmedOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        // Обычную повторную доставку события отсекаем здесь — дёшево и, главное, без исключения
        // на стороне EF (см. комментарий в ShipmentsRepository.TryCreateAsync про шум в логах).
        // Гонку двух одновременных доставок эта проверка не закрывает — для неё есть индекс.
        if (await shipmentsRepository.ExistsByOrderIdAsync(orderId, cancellationToken))
            return null;

        var now = DateTimeOffset.UtcNow;
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            TrackingNumber = GenerateTrackingNumber(),
            CurrentStatus = ShipmentStatus.Created,
            CreatedAt = now,
        };

        // Первая запись истории заводится сразу, чтобы трекинг с самого начала показывал
        // хронологию, а не пустой список рядом с уже выставленным CurrentStatus. Ключи, как и в
        // AcceptAsync, оставляем на EF — см. комментарий там.
        shipment.StatusHistory.Add(new ShipmentStatusHistory
        {
            Status = ShipmentStatus.Created,
            ChangedAt = now,
        });

        return await shipmentsRepository.TryCreateAsync(shipment, cancellationToken);
    }

    public Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return shipmentsRepository.GetByIdWithDetailsAsync(id, cancellationToken);
    }

    public async Task<ShipmentOperationResult> AcceptAsync(
        Guid id,
        ShipmentAcceptance acceptance,
        CancellationToken cancellationToken = default)
    {
        var shipment = await shipmentsRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (shipment is null)
            return ShipmentOperationResult.NotFound;

        // Приёмка — однократное событие в жизни груза, и в отличие от смены статуса она не
        // идемпотентна: повтор создал бы второй акт с другим содержимым. Поэтому строго из Created.
        if (shipment.CurrentStatus != ShipmentStatus.Created)
            return ShipmentOperationResult.Conflict;

        var now = DateTimeOffset.UtcNow;

        // Id и ShipmentId у дочерних сущностей намеренно не проставляем: их сгенерирует и
        // проставит EF при вставке. Если задать Id вручную, EF при обходе коллекции уже
        // отслеживаемого родителя решит по заполненному ключу, что строка существует, и выдаст
        // UPDATE вместо INSERT — он не найдёт ни одной строки и упадёт
        // DbUpdateConcurrencyException (поймано живым прогоном).
        shipment.Inspections.Add(new AcceptanceInspection
        {
            InspectedByUserId = acceptance.InspectedByUserId,
            PackagingCondition = acceptance.PackagingCondition,
            CargoCondition = acceptance.CargoCondition,
            Comment = acceptance.Comment,
            PhotoFileIds = [.. acceptance.PhotoFileIds],
            InspectedAt = now,
        });

        foreach (var packagingType in acceptance.PerformedPackagingTypes)
        {
            shipment.PackagingServices.Add(new PackagingService
            {
                Type = packagingType,
                PerformedByUserId = acceptance.InspectedByUserId,
                PerformedAt = now,
            });
        }

        shipment.CurrentStatus = ShipmentStatus.Accepted;
        shipment.StatusHistory.Add(new ShipmentStatusHistory
        {
            Status = ShipmentStatus.Accepted,
            ChangedAt = now,
            Location = acceptance.Location,
            Comment = acceptance.Comment,
        });

        EnqueueCargoAccepted(shipment, acceptance);

        // Приёмка публикует и CargoStatusChanged: подписчики у событий разные (CargoAccepted —
        // notification/document/logistics, CargoStatusChanged — orders-service/notification/
        // reporting), и без второго события orders-service никогда не увидел бы переход в
        // Accepted в read-модели заявки.
        EnqueueCargoStatusChanged(shipment, ShipmentStatus.Accepted, acceptance.Location);

        // Фото, приложенные прямо к приёмке, тоже уходят на анализ в ai-inspection-service.
        if (acceptance.PhotoFileIds.Count > 0)
            EnqueueCargoPhotoUploaded(shipment, acceptance.PhotoFileIds);

        // Акт, услуги упаковки, новый статус, запись истории и строки outbox уходят одним
        // SaveChanges — ни частично принятого груза, ни события без приёмки в БД не бывает.
        await shipmentsRepository.UpdateAsync(shipment, cancellationToken);
        return ShipmentOperationResult.Success;
    }

    public async Task<ShipmentOperationResult> AddPhotosAsync(
        Guid id,
        IReadOnlyCollection<Guid> photoFileIds,
        CancellationToken cancellationToken = default)
    {
        var shipment = await shipmentsRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (shipment is null)
            return ShipmentOperationResult.NotFound;

        // Фото по ТЗ живут в акте приёмки — без него их некуда положить.
        var inspection = shipment.Inspections
            .OrderByDescending(existing => existing.InspectedAt)
            .FirstOrDefault();

        if (inspection is null)
            return ShipmentOperationResult.Conflict;

        // Отсекаем повторно присланные файлы (ретрай клиента): они уже в акте, и повторный
        // CargoPhotoUploaded заставил бы ai-inspection-service анализировать их заново.
        var addedPhotoFileIds = photoFileIds.Except(inspection.PhotoFileIds).ToList();
        if (addedPhotoFileIds.Count == 0)
            return ShipmentOperationResult.Success;

        // Присваиваем новый список, а не мутируем существующий: свойство ложится в колонку uuid[],
        // и увидит ли EF правку «на месте», зависит от value comparer'а провайдера — на эту
        // деталь лучше не опираться.
        inspection.PhotoFileIds = [.. inspection.PhotoFileIds, .. addedPhotoFileIds];

        // В событие уходят только новые файлы — анализировать уже разобранные ни к чему.
        EnqueueCargoPhotoUploaded(shipment, addedPhotoFileIds);

        await shipmentsRepository.UpdateAsync(shipment, cancellationToken);
        return ShipmentOperationResult.Success;
    }

    public Task<Shipment?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        // Трек-номер приходит из адресной строки, набранный человеком: приводим к тому виду, в
        // котором он лежит в БД, иначе поиск по точному совпадению промахнётся на «cs-...» или
        // на скопированном с пробелом номере.
        var normalized = trackingNumber.Trim().ToUpperInvariant();

        return shipmentsRepository.GetByTrackingNumberAsync(normalized, cancellationToken);
    }

    public async Task<ShipmentOperationResult> ChangeStatusAsync(
        Guid id,
        ShipmentStatusChange change,
        CancellationToken cancellationToken = default)
    {
        var shipment = await shipmentsRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        if (shipment is null)
            return ShipmentOperationResult.NotFound;

        // Единственное ограничение по состоянию: выданный груз дальше не живёт. Остальные
        // переходы намеренно свободны — реальная логистика не линейна (груз возвращается на
        // склад, задерживается, снова уезжает), и жёсткий граф переходов только мешал бы
        // складу отражать факты. Недопустимые сами по себе значения (Created/Accepted) режет
        // валидатор запроса.
        if (shipment.CurrentStatus == ShipmentStatus.Delivered)
            return ShipmentOperationResult.Conflict;

        // Повтор того же статуса не отсекаем: «ВПути / Москва», затем «ВПути / Казань» — это
        // две законные отметки трекинга, а не дубликат.
        shipment.CurrentStatus = change.Status;
        shipment.StatusHistory.Add(new ShipmentStatusHistory
        {
            Status = change.Status,
            ChangedAt = DateTimeOffset.UtcNow,
            Location = change.Location,
            Comment = change.Comment,
        });

        EnqueueCargoStatusChanged(shipment, change.Status, change.Location);

        // Выдача — отдельное событие поверх смены статуса: у него свой круг подписчиков
        // (document-service выпускает закрывающие документы, reporting считает завершённые
        // доставки) и им не нужно разбирать строковый статус из CargoStatusChanged.
        if (change.Status == ShipmentStatus.Delivered)
            EnqueueCargoDelivered(shipment);

        await shipmentsRepository.UpdateAsync(shipment, cancellationToken);
        return ShipmentOperationResult.Success;
    }

    public async Task<bool> ApplyIntegrityAssessmentAsync(
        Guid shipmentId,
        PackageIntegrityAssessment assessment,
        CancellationToken cancellationToken = default)
    {
        var shipment = await shipmentsRepository.GetByIdWithDetailsAsync(shipmentId, cancellationToken);
        if (shipment is null)
            return false;

        // Вердикт приписывается к последнему акту — тому же, к которому цепляются фото,
        // по которым ИИ и работал.
        var inspection = shipment.Inspections
            .OrderByDescending(existing => existing.InspectedAt)
            .FirstOrDefault();

        if (inspection is null)
            return false;

        inspection.AiInspectionJobId = assessment.InspectionJobId;
        inspection.AiDamageDetected = assessment.DamageDetected;
        inspection.AiConfidence = assessment.Confidence;
        inspection.AiAssessedAt = DateTimeOffset.UtcNow;
        inspection.HasAssessmentDiscrepancy = IsDiscrepancy(inspection.PackagingCondition, assessment);

        await shipmentsRepository.UpdateAsync(shipment, cancellationToken);
        return true;
    }

    /// <summary>
    /// Событие называется PackageIntegrityAssessed и приходит от модели, смотрящей на фото
    /// упаковки, — поэтому сравниваем именно с <see cref="AcceptanceInspection.PackagingCondition"/>,
    /// а не с состоянием груза внутри: содержимое коробки по внешнему снимку не оценить.
    /// Расхождением считается любое несовпадение в обе стороны — и «ИИ увидел повреждение,
    /// сотрудник нет», и наоборот: второе тоже стоит перепроверить.
    /// </summary>
    private static bool IsDiscrepancy(PackagingCondition humanVerdict, PackageIntegrityAssessment assessment)
    {
        // Неуверенный вердикт расхождением не считаем. Иначе догадка модели с уверенностью 0.51
        // поднимала бы алерт на корректно принятом грузе, и склад быстро перестал бы верить флагу.
        if (assessment.Confidence < MinDiscrepancyConfidence)
            return false;

        return assessment.DamageDetected != (humanVerdict == PackagingCondition.Damaged);
    }

    private void EnqueueCargoAccepted(Shipment shipment, ShipmentAcceptance acceptance)
    {
        EnqueueEvent(new CargoAccepted
        {
            ShipmentId = shipment.Id,
            OrderId = shipment.OrderId,
            TrackingNumber = shipment.TrackingNumber,
            // Контракт возит состояния строками: подписчики (в т.ч. будущий ai-inspection-service)
            // живут в других сервисах и наших enum'ов не знают.
            PackagingCondition = acceptance.PackagingCondition.ToString(),
            CargoCondition = acceptance.CargoCondition.ToString(),
            InspectedByUserId = acceptance.InspectedByUserId,
        }, nameof(CargoAccepted));
    }

    private void EnqueueCargoStatusChanged(Shipment shipment, ShipmentStatus status, string? location)
    {
        EnqueueEvent(new CargoStatusChanged
        {
            ShipmentId = shipment.Id,
            // Поле добавлено в контракт в Фазе 4 именно ради этого: без OrderId orders-service
            // не смог бы сопоставить событие со своей заявкой.
            OrderId = shipment.OrderId,
            TrackingNumber = shipment.TrackingNumber,
            Status = status.ToString(),
            Location = location,
        }, nameof(CargoStatusChanged));
    }

    private void EnqueueCargoPhotoUploaded(Shipment shipment, IReadOnlyCollection<Guid> photoFileIds)
    {
        EnqueueEvent(new CargoPhotoUploaded
        {
            ShipmentId = shipment.Id,
            TrackingNumber = shipment.TrackingNumber,
            PhotoFileIds = [.. photoFileIds],
        }, nameof(CargoPhotoUploaded));
    }

    private void EnqueueCargoDelivered(Shipment shipment)
    {
        EnqueueEvent(new CargoDelivered
        {
            ShipmentId = shipment.Id,
            OrderId = shipment.OrderId,
            TrackingNumber = shipment.TrackingNumber,
            // ReceivedByName не заполняем: API выдачи не собирает имя получателя (отдельного
            // эндпоинта выдачи нет, статус ставится обычным POST /status). Тот же компромисс,
            // что с OrderCancelled.Reason в orders-service.
        }, nameof(CargoDelivered));
    }

    private void EnqueueEvent<TEvent>(TEvent integrationEvent, string eventName) where TEvent : IntegrationEvent
    {
        var routingKey = RabbitMqConventions.RoutingKey(ServiceName, eventName);
        var payloadJson = JsonSerializer.Serialize(integrationEvent);
        outboxWriter.Enqueue(integrationEvent.EventId, routingKey, payloadJson, integrationEvent.OccurredAtUtc);
    }

    /// <summary>
    /// Формат <c>CS-XXXXXXXXXX</c>. Намеренно отличается от номера заявки
    /// (<c>{yyyyMMdd}-{6 символов}</c> в orders-service): трек-номер живёт на публичном
    /// эндпоинте, дата в нём лишняя, а префикс не даёт спутать его с номером заявки.
    /// 32^10 комбинаций — коллизия практически невероятна, страхует уникальный индекс в БД.
    /// </summary>
    private static string GenerateTrackingNumber()
    {
        Span<char> suffix = stackalloc char[TrackingSuffixLength];
        for (var i = 0; i < suffix.Length; i++)
            suffix[i] = TrackingAlphabet[RandomNumberGenerator.GetInt32(TrackingAlphabet.Length)];

        return TrackingPrefix + new string(suffix);
    }
}
