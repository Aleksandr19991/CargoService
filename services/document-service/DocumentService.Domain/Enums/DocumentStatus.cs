namespace DocumentService.Domain.Enums;

/// <summary>Состояние документа: от «нужен» до «лежит в хранилище».</summary>
public enum DocumentStatus
{
    /// <summary>Событие получено, документ нужно сформировать.</summary>
    Pending,

    /// <summary>Документ взят в работу фоновым рабочим — строка занята, повторно её брать нельзя.</summary>
    Processing,

    /// <summary>PDF собран и сохранён в File Storage — идентификатор файла в <c>FileId</c>.</summary>
    Ready,

    /// <summary>Сформировать не удалось, причина — в <c>FailureReason</c>.</summary>
    Failed
}
