namespace DocumentService.Domain.Enums;

/// <summary>Состояние документа: от «нужен» до «лежит в хранилище».</summary>
public enum DocumentStatus
{
    /// <summary>Событие получено, документ нужно сформировать.</summary>
    Pending,

    /// <summary>PDF собран и сохранён в File Storage — идентификатор файла в <c>FileId</c>.</summary>
    Ready,

    /// <summary>Сформировать не удалось, причина — в <c>FailureReason</c>.</summary>
    Failed
}
