using Mapster;

namespace FileStorageService.API.Mapping;

/// <summary>
/// Заглушка ради единообразия с остальными сервисами: собственных пар «сущность ↔ DTO» здесь нет —
/// сервис отдаёт ровно то, что вернуло хранилище, и ответы собираются вручную в контроллере.
/// </summary>
public class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
    }
}
