using FileStorageService.API.Models.Requests;
using FileStorageService.Application.Models;
using FluentValidation;

namespace FileStorageService.API.Validators;

public class CreateUploadUrlRequestValidator : AbstractValidator<CreateUploadUrlRequest>
{
    public CreateUploadUrlRequestValidator(FileUploadPolicy policy)
    {
        // Тип отсекаем здесь, до обращения к хранилищу: не тратить round-trip на заведомо
        // неразрешённый файл и дать понятную ошибку вместо отказа от MinIO. Само ограничение
        // при этом дублируется в подписанной policy — валидатор можно обойти, подпись нет.
        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(policy.IsContentTypeAllowed)
            .WithMessage(_ => $"Тип файла не разрешён. Допустимые: {string.Join(", ", policy.AllowedContentTypes)}.");
    }
}
