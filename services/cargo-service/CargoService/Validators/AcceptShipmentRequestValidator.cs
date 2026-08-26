using CargoService.API.Models.Requests;
using CargoService.Application.Interfaces;
using FluentValidation;

namespace CargoService.API.Validators;

public class AcceptShipmentRequestValidator : AbstractValidator<AcceptShipmentRequest>
{
    public AcceptShipmentRequestValidator(IFileStorageClient fileStorageClient)
    {
        RuleFor(x => x.PackagingCondition).IsInEnum();
        RuleFor(x => x.CargoCondition).IsInEnum();
        RuleFor(x => x.Comment).MaximumLength(1000);
        RuleFor(x => x.Location).MaximumLength(100);
        RuleForEach(x => x.PhotoFileIds).NotEmpty();
        RuleForEach(x => x.PerformedPackagingTypes).IsInEnum();

        // Без этой проверки в акт приёмки — документ о состоянии груза — можно было записать
        // любой случайный GUID как «фото». Проверяем в file-storage-service, что за каждым
        // идентификатором действительно лежит файл.
        RuleFor(x => x.PhotoFileIds)
            .MustAsync(async (photoFileIds, cancellationToken) =>
                (await fileStorageClient.FindMissingAsync(photoFileIds, cancellationToken)).Count == 0)
            .WithMessage("Часть переданных файлов отсутствует в хранилище.")
            .When(x => x.PhotoFileIds.Count > 0);
    }
}
