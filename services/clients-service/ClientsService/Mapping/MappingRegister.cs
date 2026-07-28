using ClientsService.API.Models.Requests;
using ClientsService.API.Models.Responses;
using ClientsService.Domain.Entities;
using Mapster;

namespace ClientsService.API.Mapping;

/// <summary>
/// Every request/response DTO ↔ domain entity mapping lives here, not in controllers. Discovered
/// and applied automatically by <c>services.AddMapster()</c> in Program.cs (Mapster scans the
/// assembly for <see cref="IRegister"/> implementations).
/// </summary>
public class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Counterparty, CounterpartyResponse>();
        config.NewConfig<CreateCounterpartyRequest, Counterparty>();
        config.NewConfig<UpdateCounterpartyRequest, Counterparty>();
    }
}
