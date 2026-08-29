namespace DocumentService.Domain.Enums;

/// <summary>Виды документов, которые формирует сервис (spec.md §3.1).</summary>
public enum DocumentType
{
    /// <summary>Транспортная накладная — сопроводительный документ на перевозку.</summary>
    Waybill,

    /// <summary>Акт приёма-передачи: чем и в каком состоянии груз принят складом.</summary>
    AcceptanceAct,

    /// <summary>Акт осмотра при повреждении — составляется, когда повреждение зафиксировано.</summary>
    DamageInspectionAct
}
