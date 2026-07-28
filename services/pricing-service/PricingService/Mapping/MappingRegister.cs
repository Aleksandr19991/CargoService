using Mapster;
using PricingService.API.Models.Requests;
using PricingService.API.Models.Responses;
using PricingService.Application.Models;

namespace PricingService.API.Mapping;

/// <summary>
/// Every request/response DTO ↔ Application-model mapping lives here, not in controllers.
/// Discovered and applied automatically by <c>services.AddMapster()</c> in Program.cs (Mapster
/// scans the assembly for <see cref="IRegister"/> implementations).
/// </summary>
public class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<PriceCalculationRequest, PriceCalculationInput>()
            .Map(dest => dest.ShippingTypeCode, src => src.ShippingType.ToString())
            .Map(dest => dest.PackagingTypeCode, src => src.PackagingType.ToString());

        config.NewConfig<PriceBreakdownLine, PriceBreakdownItemResponse>();
        config.NewConfig<PriceCalculationResult, PriceCalculationResponse>();
    }
}
