using OrdersService.Domain.Enums;

namespace OrdersService.Domain.Entities;

/// <summary>Order.ServiceOptions (see spec.md §2.4) — an EF Core owned type, not its own table.</summary>
public class OrderServiceOptions
{
    public ShippingType ShippingType { get; set; } = ShippingType.Standard;
    public PackagingType PackagingType { get; set; }
    public bool NeedsPickup { get; set; }
    public bool NeedsDelivery { get; set; }
    public bool NeedsInsurance { get; set; }
}
