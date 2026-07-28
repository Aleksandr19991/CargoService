namespace PricingService.Domain.Enums;

public enum TariffCategory
{
    ShippingType,
    PackagingType,
    PickupDelivery,
    Insurance,

    // Per-kg/per-km base freight rates used by pricing.calculate to price the shipment itself,
    // before the ShippingType/PackagingType/PickupDelivery/Insurance surcharges above are added.
    BaseRate
}
