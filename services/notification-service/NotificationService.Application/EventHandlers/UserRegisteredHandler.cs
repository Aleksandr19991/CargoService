using CargoService.Contracts.Events.V1;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.EventHandlers;

/// <summary>
/// Единственный обработчик, который ничего не отправляет: он наполняет read-модель контактов,
/// без которой отправлять было бы некуда (см. <see cref="NotificationRecipient"/>).
/// Приветственное письмо при регистрации в списке уведомлений ТЗ (§2.6) не значится.
/// </summary>
public class UserRegisteredHandler(IRecipientsRepository recipientsRepository) : IEventHandler<UserRegistered>
{
    public Task HandleAsync(UserRegistered @event, CancellationToken cancellationToken) =>
        recipientsRepository.UpsertRecipientAsync(
            new NotificationRecipient
            {
                UserId = @event.UserId,
                Name = @event.Name,
                LastName = @event.LastName,
                Email = @event.Email,
                Phone = @event.Phone,
            },
            cancellationToken);
}
