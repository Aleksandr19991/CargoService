namespace CargoService.Application.Interfaces;

/// <summary>
/// Доступ к file-storage-service. Байты через cargo-service не ходят — он лишь проверяет, что
/// идентификаторы файлов, которые ему передали, действительно существуют в хранилище.
/// </summary>
public interface IFileStorageClient
{
    /// <summary>
    /// Возвращает те из переданных идентификаторов, которых в хранилище нет. Пустой результат —
    /// все файлы на месте.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> FindMissingAsync(
        IReadOnlyCollection<Guid> fileIds,
        CancellationToken cancellationToken = default);
}
