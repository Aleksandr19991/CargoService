using CargoService.Application.Models;
using CargoService.Domain.Entities;

namespace CargoService.Application.Interfaces;

public interface IShipmentsService
{
    /// <summary>
    /// Заводит груз по подтверждённой заявке: генерирует трек-номер, ставит статус
    /// <c>Created</c> и первую запись в историю статусов. Идемпотентна — возвращает
    /// <c>null</c>, если груз по этой заявке уже существует (повторная доставка OrderConfirmed).
    /// </summary>
    Task<Shipment?> CreateFromConfirmedOrderAsync(Guid orderId, DateTimeOffset? deliveryDeadline, CancellationToken cancellationToken = default);

    /// <summary>Полная карточка груза со всеми актами, услугами упаковки и историей статусов.</summary>
    Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Приёмка груза сотрудником: акт с состоянием упаковки/груза и фото, записи о выполненных
    /// услугах упаковки, перевод в статус <c>Accepted</c> с записью в историю. Принять можно
    /// только груз в статусе <c>Created</c> — повторная приёмка даёт <c>Conflict</c>.
    /// </summary>
    Task<ShipmentOperationResult> AcceptAsync(Guid id, ShipmentAcceptance acceptance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавляет идентификаторы фото к акту приёмки. Требует уже принятого груза — без акта
    /// прикладывать фото некуда, поэтому у непринятого груза возвращает <c>Conflict</c>.
    /// </summary>
    Task<ShipmentOperationResult> AddPhotosAsync(Guid id, IReadOnlyCollection<Guid> photoFileIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Переводит груз в новый статус и дописывает запись в историю. Повтор того же статуса —
    /// не ошибка: несколько отметок «ВПути» с разными локациями это и есть трекинг. Из
    /// <c>Delivered</c> переходов нет (терминальный статус) — там <c>Conflict</c>. Запрет на
    /// ручную установку <c>Created</c>/<c>Accepted</c> живёт в валидаторе запроса: это
    /// недопустимые значения сами по себе, а не следствие текущего состояния.
    /// </summary>
    Task<ShipmentOperationResult> ChangeStatusAsync(Guid id, ShipmentStatusChange change, CancellationToken cancellationToken = default);

    /// <summary>
    /// Груз по трек-номеру для публичного трекинга. Номер нормализуется (регистр и пробелы) —
    /// его вбивают руками, а хранится он в верхнем регистре.
    /// </summary>
    Task<Shipment?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает вердикт ai-inspection-service в акт приёмки и выставляет флаг расхождения с
    /// оценкой сотрудника. Возвращает <c>false</c>, если груза нет или он ещё не принят (акта,
    /// куда приписать вердикт, не существует) — для консьюмера это перманентное несоответствие,
    /// а не временный сбой.
    /// </summary>
    Task<bool> ApplyIntegrityAssessmentAsync(Guid shipmentId, PackageIntegrityAssessment assessment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Переводит просроченные по сроку доставки грузы в статус <c>Delayed</c> с записью в историю
    /// и публикацией <c>CargoStatusChanged</c>. Возвращает число помеченных за проход. Вызывается
    /// фоновой джобой контроля SLA.
    /// </summary>
    Task<int> FlagOverdueShipmentsAsync(int batchSize, CancellationToken cancellationToken = default);
}
