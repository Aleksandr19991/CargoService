using DocumentService.API.Models.Responses;
using DocumentService.Domain.Entities;
using Mapster;

namespace DocumentService.API.Mapping;

/// <summary>
/// Every request/response DTO ↔ domain entity mapping lives here, not in controllers. Discovered
/// and applied automatically by <c>services.AddMapster()</c> in Program.cs (Mapster scans the
/// assembly for <see cref="IRegister"/> implementations).
/// </summary>
public class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // Обстоятельства приёмки и выдачи (состояния, получатель) наружу не отдаются: они
        // напечатаны в самом документе, а список нужен для навигации, а не для чтения акта.
        config.NewConfig<Document, DocumentResponse>();
    }
}
