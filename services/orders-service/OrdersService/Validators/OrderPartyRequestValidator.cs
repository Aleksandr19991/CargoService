using FluentValidation;
using OrdersService.API.Models.Requests;

namespace OrdersService.API.Validators;

public class OrderPartyRequestValidator : AbstractValidator<OrderPartyRequest>
{
    public OrderPartyRequestValidator()
    {
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OrganizationOrPersonName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}
