using CargoService.API.Models.Requests;
using CargoService.Application.Interfaces;
using FluentValidation;

namespace CargoService.API.Validators;

public class AddShipmentPhotosRequestValidator : AbstractValidator<AddShipmentPhotosRequest>
{
    public AddShipmentPhotosRequestValidator(IFileStorageClient fileStorageClient)
    {
        RuleFor(x => x.PhotoFileIds).NotEmpty();
        RuleForEach(x => x.PhotoFileIds).NotEmpty();

        // Та же проверка, что и при приёмке: ссылки на несуществующие файлы в акте не нужны.
        RuleFor(x => x.PhotoFileIds)
            .MustAsync(async (photoFileIds, cancellationToken) =>
                (await fileStorageClient.FindMissingAsync(photoFileIds, cancellationToken)).Count == 0)
            .WithMessage("Часть переданных файлов отсутствует в хранилище.")
            .When(x => x.PhotoFileIds.Count > 0);
    }
}
