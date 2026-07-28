using ClientsService.API.Models.Requests;
using ClientsService.Domain.Enums;
using FluentValidation;

namespace ClientsService.API.Validators;

public class CreateCounterpartyRequestValidator : AbstractValidator<CreateCounterpartyRequest>
{
    public CreateCounterpartyRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();

        When(x => x.Type == CounterpartyType.Organization, () =>
        {
            RuleFor(x => x.OrganizationName).NotEmpty().MaximumLength(200);
        });

        When(x => x.Type == CounterpartyType.Individual, () =>
        {
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        });

        RuleFor(x => x.Inn)
            .Matches(@"^\d{10}(\d{2})?$")
            .When(x => !string.IsNullOrEmpty(x.Inn));

        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(255).EmailAddress();
    }
}
