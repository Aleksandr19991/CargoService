using System.Security.Cryptography;
using CargoService.Application.Interfaces;
using CargoService.Application.Models;
using CargoService.Domain.Entities;
using CargoService.Domain.Enums;

namespace CargoService.Application;

public class ShipmentsService(IShipmentsRepository shipmentsRepository) : IShipmentsService
{
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

        // Акт, услуги упаковки, новый статус и запись истории уходят одним SaveChanges —
        // частично принятого груза в БД не бывает.
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

        // Присваиваем новый список, а не мутируем существующий: свойство ложится в колонку uuid[],
        // и увидит ли EF правку «на месте», зависит от value comparer'а провайдера — на эту
        // деталь лучше не опираться. Заодно отсекаем повторно присланные файлы (ретрай клиента):
        // дубликаты в списке дадут только лишние CargoPhotoUploaded в будущем.
        inspection.PhotoFileIds = [.. inspection.PhotoFileIds.Union(photoFileIds)];

        await shipmentsRepository.UpdateAsync(shipment, cancellationToken);
        return ShipmentOperationResult.Success;
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
