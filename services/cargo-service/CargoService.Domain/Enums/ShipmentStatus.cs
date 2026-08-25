namespace CargoService.Domain.Enums;

/// <summary>
/// Полный жизненный цикл груза из ТЗ (spec.md §2.5). Значения англоязычные — как
/// <c>OrderStatus</c> в orders-service; русские названия из ТЗ приведены в комментариях.
/// </summary>
public enum ShipmentStatus
{
    /// <summary>Создана — Shipment заведён по событию OrderConfirmed, груз ещё не принят.</summary>
    Created,

    /// <summary>Принята — сотрудник принял груз и зафиксировал состояние.</summary>
    Accepted,

    /// <summary>НаСкладе.</summary>
    InWarehouse,

    /// <summary>ВПути.</summary>
    InTransit,

    /// <summary>Задерживается — в том числе выставляется автоматически джобой контроля SLA.</summary>
    Delayed,

    /// <summary>ПрибылаВГородНазначения.</summary>
    ArrivedAtDestination,

    /// <summary>ГотовКВыдаче.</summary>
    ReadyForPickup,

    /// <summary>Выдана.</summary>
    Delivered,

    /// <summary>Проблема.</summary>
    Problem
}
