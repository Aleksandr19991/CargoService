using FluentValidation;
using IdentityService.API.Models.Requests;

namespace IdentityService.API.Validators;

public class CreateStaffUserRequestValidator : AbstractValidator<CreateStaffUserRequest>
{
    public CreateStaffUserRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(255).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.Role).IsInEnum();
    }
}
