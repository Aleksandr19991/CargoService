using FluentValidation;
using IdentityService.API.Models.Requests;

namespace IdentityService.API.Validators;

public class ChangeUserRoleRequestValidator : AbstractValidator<ChangeUserRoleRequest>
{
    public ChangeUserRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}
