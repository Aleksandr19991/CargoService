using FluentValidation;
using PaymentService.API.Models.Requests;

namespace PaymentService.API.Validators;

public class PaymentWebhookRequestValidator : AbstractValidator<PaymentWebhookRequest>
{
    public PaymentWebhookRequestValidator()
    {
        RuleFor(request => request.Event).NotEmpty();

        RuleFor(request => request.Payment).NotNull();

        // Незнакомый статус проверкой не отсекается: словарь провайдера расширяется, и уведомление
        // о неизвестном событии — повод его не применять (сценарий сочтёт его промежуточным), а не
        // повод отвечать ошибкой и заставлять провайдера присылать его снова.
        RuleFor(request => request.Payment.Id)
            .NotEmpty()
            .MaximumLength(100)
            .When(request => request.Payment is not null);

        RuleFor(request => request.Payment.Status)
            .NotEmpty()
            .When(request => request.Payment is not null);
    }
}
