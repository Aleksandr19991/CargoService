using FileStorageService.API.Models.Requests;
using FluentValidation;

namespace FileStorageService.API.Validators;

public class CreateUploadUrlRequestValidator : AbstractValidator<CreateUploadUrlRequest>
{
    public CreateUploadUrlRequestValidator()
    {
        // Здесь только формальная проверка. Белый список разрешённых типов и лимит размера —
        // задача 2 Фазы 11.
        RuleFor(x => x.ContentType)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(@"^[-\w.+]+/[-\w.+]+$")
            .WithMessage("ContentType должен быть MIME-типом вида type/subtype.");
    }
}
