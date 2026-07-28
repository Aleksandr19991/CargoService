namespace PricingService.API.Models.Requests;

// Member names match TariffRate.Code values in the PackagingType category exactly (see
// PricingService.Application.TariffCodes) — the mapping from request to calculation input is a
// plain ToString(), no lookup table needed.
public enum PackagingTypeOption
{
    Wooden,
    Pallet,
    Special
}
