using Mapster;
using OrdersService.API.Models.Requests;
using OrdersService.API.Models.Responses;
using OrdersService.Domain.Entities;

namespace OrdersService.API.Mapping;

/// <summary>
/// Every request/response DTO ↔ domain entity mapping lives here, not in controllers. Discovered
/// and applied automatically by <c>services.AddMapster()</c> in Program.cs (Mapster scans the
/// assembly for <see cref="IRegister"/> implementations).
/// </summary>
public class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<OrderPartyRequest, OrderParty>();

        // CreateOrderRequest carries ShippingType/PackagingType/NeedsPickup/NeedsDelivery/
        // NeedsInsurance flat at the top level, but Order nests them under ServiceOptions — same
        // flatten/unflatten mismatch already handled this way in pricing-service's MappingRegister.
        config.NewConfig<CreateOrderRequest, Order>()
            .Map(dest => dest.ServiceOptions.ShippingType, src => src.ShippingType)
            .Map(dest => dest.ServiceOptions.PackagingType, src => src.PackagingType)
            .Map(dest => dest.ServiceOptions.NeedsPickup, src => src.NeedsPickup)
            .Map(dest => dest.ServiceOptions.NeedsDelivery, src => src.NeedsDelivery)
            .Map(dest => dest.ServiceOptions.NeedsInsurance, src => src.NeedsInsurance);

        config.NewConfig<OrderParty, OrderPartyResponse>();
        config.NewConfig<OrderServiceOptions, OrderServiceOptionsResponse>();
        config.NewConfig<Order, OrderResponse>();
    }
}
