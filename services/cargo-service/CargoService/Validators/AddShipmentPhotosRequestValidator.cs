using CargoService.API.Models.Requests;
using FluentValidation;

namespace CargoService.API.Validators;

public class AddShipmentPhotosRequestValidator : AbstractValidator<AddShipmentPhotosRequest>
{
    public AddShipmentPhotosRequestValidator()
    {
        RuleFor(x => x.PhotoFileIds).NotEmpty();
        RuleForEach(x => x.PhotoFileIds).NotEmpty();
    }
}
