using CargoService.API.Models.Requests;
using FluentValidation;

namespace CargoService.API.Validators;

public class AcceptShipmentRequestValidator : AbstractValidator<AcceptShipmentRequest>
{
    public AcceptShipmentRequestValidator()
    {
        RuleFor(x => x.PackagingCondition).IsInEnum();
        RuleFor(x => x.CargoCondition).IsInEnum();
        RuleFor(x => x.Comment).MaximumLength(1000);
        RuleFor(x => x.Location).MaximumLength(100);
        RuleForEach(x => x.PhotoFileIds).NotEmpty();
        RuleForEach(x => x.PerformedPackagingTypes).IsInEnum();
    }
}
