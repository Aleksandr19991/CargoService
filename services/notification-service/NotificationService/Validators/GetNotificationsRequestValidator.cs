using FluentValidation;
using NotificationService.API.Models.Requests;

namespace NotificationService.API.Validators;

public class GetNotificationsRequestValidator : AbstractValidator<GetNotificationsRequest>
{
    public GetNotificationsRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
