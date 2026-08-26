using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;

namespace NotificationService.Persistence.Repositories;

public class RecipientsRepository(AppDbContext dbContext) : IRecipientsRepository
{
    public Task<NotificationRecipient?> GetRecipientAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.NotificationRecipients
            .AsNoTracking()
            .FirstOrDefaultAsync(recipient => recipient.UserId == userId, cancellationToken);

    public Task<OrderRecipient?> GetOrderRecipientAsync(Guid orderId, CancellationToken cancellationToken) =>
        dbContext.OrderRecipients
            .AsNoTracking()
            .FirstOrDefaultAsync(orderRecipient => orderRecipient.OrderId == orderId, cancellationToken);

    public async Task UpsertRecipientAsync(NotificationRecipient recipient, CancellationToken cancellationToken)
    {
        // Читается с отслеживанием: найденную строку правим на месте, иначе EF попытается
        // вставить вторую с тем же ключом. То же и в UpsertOrderRecipientAsync.
        var existing = await dbContext.NotificationRecipients
            .FirstOrDefaultAsync(stored => stored.UserId == recipient.UserId, cancellationToken);

        if (existing is null)
        {
            await dbContext.NotificationRecipients.AddAsync(recipient, cancellationToken);
        }
        else
        {
            existing.Name = recipient.Name;
            existing.LastName = recipient.LastName;
            existing.Email = recipient.Email;
            existing.Phone = recipient.Phone;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<NotificationPreference?> GetPreferenceAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(preference => preference.UserId == userId, cancellationToken);

    public async Task UpsertPreferenceAsync(NotificationPreference preference, CancellationToken cancellationToken)
    {
        var existing = await dbContext.NotificationPreferences
            .FirstOrDefaultAsync(stored => stored.UserId == preference.UserId, cancellationToken);

        if (existing is null)
        {
            await dbContext.NotificationPreferences.AddAsync(preference, cancellationToken);
        }
        else
        {
            existing.EmailEnabled = preference.EmailEnabled;
            existing.SmsEnabled = preference.SmsEnabled;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertOrderRecipientAsync(OrderRecipient orderRecipient, CancellationToken cancellationToken)
    {
        var existing = await dbContext.OrderRecipients
            .FirstOrDefaultAsync(stored => stored.OrderId == orderRecipient.OrderId, cancellationToken);

        if (existing is null)
        {
            await dbContext.OrderRecipients.AddAsync(orderRecipient, cancellationToken);
        }
        else
        {
            existing.RecipientUserId = orderRecipient.RecipientUserId;
            existing.OrderNumber = orderRecipient.OrderNumber;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
