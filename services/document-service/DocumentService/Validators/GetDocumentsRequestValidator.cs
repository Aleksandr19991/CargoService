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

        // Явное сравнение с Guid.Empty, а не NotEmpty(): у Guid? «пустым» для FluentValidation
        // считается только null, и нули в идентификаторе прошли бы проверку — а это ошибка
        // вызывающего, которую лучше показать, чем ответить пустым списком (поймано тестом).
        RuleFor(request => request.OrderId)
            .Must(orderId => orderId != Guid.Empty)
            .When(request => request.OrderId.HasValue)
            .WithMessage("orderId не может быть нулевым идентификатором.");

        RuleFor(request => request.ShipmentId)
            .Must(shipmentId => shipmentId != Guid.Empty)
            .When(request => request.ShipmentId.HasValue)
            .WithMessage("shipmentId не может быть нулевым идентификатором.");
    }
}
