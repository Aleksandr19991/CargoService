using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PricingService.API.Filters;

/// <summary>
/// Validates every action argument that has a registered FluentValidation <see cref="IValidator{T}"/>
/// before the action runs. Adding an AbstractValidator<TRequest> for a request DTO is
/// enough to enforce it — no per-action validation code needed. Registered globally in Program.cs
/// via options.Filters.AddValidationFilter().
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue;

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext);
            if (result.IsValid)
                continue;

            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
            return;
        }

        await next();
    }
}

public static class ValidationFilterExtensions
{
    public static FilterCollection AddValidationFilter(this FilterCollection filters)
    {
        filters.Add<ValidationFilter>();
        return filters;
    }
}
