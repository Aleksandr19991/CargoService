namespace NotificationService.Application;

/// <summary>
/// Коды шаблонов уведомлений. Каждый код — имя события, по которому уведомление отправляется:
/// консьюмеры (Фаза 6, задача 4) ищут шаблон по коду входящего события, и <c>nameof</c> вместо
/// строкового литерала делает связь проверяемой компилятором — переименование события в
/// <c>CargoService.Contracts</c> сломает сборку здесь, а не поиск шаблона в рантайме.
/// Общий набор с seed-данными в Persistence (NotificationTemplateConfiguration).
/// </summary>
public static class NotificationTemplateCodes
{
    public const string OrderCreated = nameof(CargoService.Contracts.Events.V1.OrderCreated);
    public const string OrderConfirmed = nameof(CargoService.Contracts.Events.V1.OrderConfirmed);
    public const string OrderCancelled = nameof(CargoService.Contracts.Events.V1.OrderCancelled);

    public const string CargoAccepted = nameof(CargoService.Contracts.Events.V1.CargoAccepted);
    public const string CargoStatusChanged = nameof(CargoService.Contracts.Events.V1.CargoStatusChanged);
    public const string CargoDelivered = nameof(CargoService.Contracts.Events.V1.CargoDelivered);

    public const string PaymentCompleted = nameof(CargoService.Contracts.Events.V1.PaymentCompleted);
    public const string PaymentFailed = nameof(CargoService.Contracts.Events.V1.PaymentFailed);

    public const string DocumentGenerated = nameof(CargoService.Contracts.Events.V1.DocumentGenerated);

    /// <summary>
    /// Единственный код для сотрудников, а не для клиента: расхождение оценки упаковки —
    /// внутренний повод перепроверить груз (см. таблицу событий в spec.md §4).
    /// </summary>
    public const string PackageIntegrityAssessed = nameof(CargoService.Contracts.Events.V1.PackageIntegrityAssessed);
}
