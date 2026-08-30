using DocumentService.API.Models.Requests;
using FluentValidation;

namespace DocumentService.API.Validators;

public class GetDocumentsRequestValidator : AbstractValidator<GetDocumentsRequest>
{
    public GetDocumentsRequestValidator()
    {
        RuleFor(request => request)
            .Must(request => request.OrderId.HasValue ^ request.ShipmentId.HasValue)
            .WithMessage("Укажите либо orderId, либо shipmentId — ровно один из двух.");

        RuleFor(request => request.OrderId).NotEmpty().When(request => request.OrderId.HasValue);
        RuleFor(request => request.ShipmentId).NotEmpty().When(request => request.ShipmentId.HasValue);
    }
}
