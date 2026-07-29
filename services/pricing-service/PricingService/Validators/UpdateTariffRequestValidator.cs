using FluentValidation;
using PricingService.API.Models.Requests;

namespace PricingService.API.Validators;

public class UpdateTariffRequestValidator : AbstractValidator<UpdateTariffRequest>
{
    public UpdateTariffRequestValidator()
    {
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}
