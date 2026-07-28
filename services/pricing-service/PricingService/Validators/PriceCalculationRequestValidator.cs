using FluentValidation;
using PricingService.API.Models.Requests;

namespace PricingService.API.Validators;

public class PriceCalculationRequestValidator : AbstractValidator<PriceCalculationRequest>
{
    public PriceCalculationRequestValidator()
    {
        RuleFor(x => x.WeightKg).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VolumeM3).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DistanceKm).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ShippingType).IsInEnum();
        RuleFor(x => x.PackagingType).IsInEnum();

        When(x => x.NeedsInsurance, () =>
        {
            RuleFor(x => x.DeclaredValue).NotNull().GreaterThan(0);
        });
    }
}
