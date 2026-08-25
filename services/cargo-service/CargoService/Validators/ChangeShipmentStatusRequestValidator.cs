using CargoService.API.Models.Requests;
using CargoService.Domain.Enums;
using FluentValidation;

namespace CargoService.API.Validators;

public class ChangeShipmentStatusRequestValidator : AbstractValidator<ChangeShipmentStatusRequest>
{
    // Эти два статуса выставляют только создание груза (по OrderConfirmed) и эндпоинт приёмки —
    // у последнего с составлением акта. Разреши их здесь, и статус Accepted можно было бы
    // получить в обход акта приёмки. Запрет живёт в валидаторе, а не в сервисе, потому что это
    // всегда недопустимые значения, а не следствие текущего состояния груза (400, не 409).
    private static readonly ShipmentStatus[] NotManuallyAssignable =
        [ShipmentStatus.Created, ShipmentStatus.Accepted];

    public ChangeShipmentStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .Must(status => !NotManuallyAssignable.Contains(status))
            .WithMessage("Статусы Created и Accepted выставляются созданием груза и приёмкой, вручную их назначить нельзя.");

        RuleFor(x => x.Location).MaximumLength(100);
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}
