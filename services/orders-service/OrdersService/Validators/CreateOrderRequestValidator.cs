using FluentValidation;
using OrdersService.API.Models.Requests;

namespace OrdersService.API.Validators;

public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.Sender).SetValidator(new OrderPartyRequestValidator());
        RuleFor(x => x.Recipient).SetValidator(new OrderPartyRequestValidator());

        RuleFor(x => x.OriginCity).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DestinationCity).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DistanceKm).GreaterThanOrEqualTo(0);

        RuleFor(x => x.CargoName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CargoWeight).GreaterThan(0);
        RuleFor(x => x.CargoVolumeM3).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CargoQuantity).GreaterThan(0);

        RuleFor(x => x.DeclaredValue).GreaterThanOrEqualTo(0);

        RuleFor(x => x.ShippingType).IsInEnum();
        RuleFor(x => x.PackagingType).IsInEnum();

        When(x => x.RequestedShipDate is not null && x.DeliveryDeadline is not null, () =>
        {
            RuleFor(x => x.DeliveryDeadline).GreaterThanOrEqualTo(x => x.RequestedShipDate);
        });

        When(x => x.NeedsInsurance, () =>
        {
            RuleFor(x => x.DeclaredValue).GreaterThan(0);
        });
    }
}
