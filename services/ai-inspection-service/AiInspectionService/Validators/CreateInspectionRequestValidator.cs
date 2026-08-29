using AiInspectionService.API.Models.Requests;
using FluentValidation;

namespace AiInspectionService.API.Validators;

public class CreateInspectionRequestValidator : AbstractValidator<CreateInspectionRequest>
{
    public CreateInspectionRequestValidator()
    {
        RuleFor(request => request.ShipmentId).NotEmpty();

        RuleFor(request => request.PhotoFileIds)
            .NotEmpty()
            .WithMessage("Нужен хотя бы один снимок: задание без снимков проверять нечем.");

        RuleForEach(request => request.PhotoFileIds).NotEmpty();

        // Существование файлов здесь не проверяется: конвейер всё равно скачивает их сам и
        // закрывает задание с причиной «снимок отсутствует в хранилище», а проверка на входе
        // ту же ошибку не исключит — файл можно удалить между запросом и инференсом.
    }
}
