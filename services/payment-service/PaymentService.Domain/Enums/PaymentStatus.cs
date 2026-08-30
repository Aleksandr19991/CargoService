namespace PaymentService.Domain.Enums;

/// <summary>
/// Состояние одной попытки оплаты. <see cref="Failed"/> покрывает оба вида неудачи — и отказ
/// провайдера завести платёж, и отменённый им платёж (не хватило средств, отказ банка, истекло
/// время): для сервиса это один и тот же исход «деньги не пришли», а чем именно он вызван,
/// сказано в <c>FailureReason</c>.
/// </summary>
public enum PaymentStatus
{
    /// <summary>Платёж заведён у провайдера, клиент его ещё не оплатил.</summary>
    Pending = 0,

    /// <summary>Деньги получены.</summary>
    Succeeded = 1,

    /// <summary>Оплата не состоялась.</summary>
    Failed = 2,
}
