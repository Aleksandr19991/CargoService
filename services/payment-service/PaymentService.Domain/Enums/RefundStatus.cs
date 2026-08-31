namespace PaymentService.Domain.Enums;

/// <summary>Состояние возврата — тот же жизненный цикл, что у платежа, но в обратную сторону.</summary>
public enum RefundStatus
{
    /// <summary>Возврат заведён у провайдера, но деньги ещё не дошли.</summary>
    Pending = 0,

    /// <summary>Деньги возвращены.</summary>
    Succeeded = 1,

    /// <summary>Вернуть не удалось — причина в <c>FailureReason</c>.</summary>
    Failed = 2,
}
