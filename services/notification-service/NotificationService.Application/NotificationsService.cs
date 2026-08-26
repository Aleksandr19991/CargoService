using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application;

/// <summary>
/// Отправка уведомления по поводу: шаблоны повода → доступные каналы получателя → отправка →
/// запись каждой попытки в историю.
/// </summary>
public class NotificationsService(
    IRecipientsRepository recipientsRepository,
    INotificationTemplatesRepository templatesRepository,
    INotificationLogsRepository logsRepository,
    INotificationSenderRegistry senderRegistry,
    NotificationOptions options,
    ILogger<NotificationsService> logger) : INotificationsService
{
    public async Task SendAsync(NotificationTrigger trigger, CancellationToken cancellationToken)
    {
        var templates = await templatesRepository.GetByCodeAsync(trigger.TemplateCode, cancellationToken);
        if (templates.Count == 0)
        {
            // Повод есть, а текста для него нет — рассогласование шаблонов с кодом события.
            // Исключение здесь только загнало бы сообщение в DLQ, откуда его всё равно
            // некому переотправить, пока шаблон не заведут.
            logger.LogWarning("No templates found for {TemplateCode}, notification skipped", trigger.TemplateCode);
            return;
        }

        var recipient = await ResolveRecipientAsync(trigger, cancellationToken);
        if (recipient is null)
            return;

        var placeholders = WithOrderNumber(trigger.Placeholders, recipient.OrderNumber);
        var logs = new List<NotificationLog>();

        foreach (var template in templates)
        {
            var contact = ContactFor(template.Channel, recipient);
            if (string.IsNullOrWhiteSpace(contact))
            {
                logger.LogInformation(
                    "Recipient has no {Channel} contact, {TemplateCode} not sent over this channel",
                    template.Channel, trigger.TemplateCode);
                continue;
            }

            var sender = senderRegistry.Resolve(template.Channel);
            if (sender is null)
            {
                // Шаблон канала есть, интеграции ещё нет (см. INotificationSenderRegistry).
                logger.LogInformation(
                    "No sender registered for {Channel}, {TemplateCode} not sent over this channel",
                    template.Channel, trigger.TemplateCode);
                continue;
            }

            WarnAboutUnresolvedPlaceholders(template, placeholders);

            var subject = template.Subject is null ? null : TemplateRenderer.Render(template.Subject, placeholders);
            var body = TemplateRenderer.Render(template.Body, placeholders);

            var result = await sender.SendAsync(
                new NotificationMessage
                {
                    Channel = template.Channel,
                    RecipientContact = contact,
                    Subject = subject,
                    Body = body,
                },
                cancellationToken);

            var sentAt = DateTimeOffset.UtcNow;
            logs.Add(new NotificationLog
            {
                Id = Guid.NewGuid(),
                RecipientUserId = recipient.UserId,
                RecipientContact = contact,
                Channel = template.Channel,
                TemplateCode = trigger.TemplateCode,
                Subject = subject,
                Body = body,
                Status = result.IsSuccess ? NotificationStatus.Sent : NotificationStatus.Failed,
                CreatedAt = sentAt,
                SentAt = result.IsSuccess ? sentAt : null,
                FailureReason = result.FailureReason,
                RelatedEntityType = trigger.RelatedEntityType,
                RelatedEntityId = trigger.RelatedEntityId,
            });
        }

        if (logs.Count > 0)
            await logsRepository.AddRangeAsync(logs, cancellationToken);
    }

    /// <summary>
    /// Кому слать: сотрудникам на общий адрес из конфигурации либо владельцу заявки — его
    /// контакты приезжают событием <c>UserRegistered</c>, а связка «заявка → владелец» —
    /// событием <c>OrderCreated</c>.
    /// </summary>
    private async Task<ResolvedRecipient?> ResolveRecipientAsync(
        NotificationTrigger trigger,
        CancellationToken cancellationToken)
    {
        if (trigger.ToStaff)
        {
            if (string.IsNullOrWhiteSpace(options.StaffEmail))
            {
                logger.LogWarning(
                    "Notifications:StaffEmail is not configured, staff notification {TemplateCode} skipped",
                    trigger.TemplateCode);
                return null;
            }

            // У служебного адреса нет ни пользователя, ни телефона — только почта.
            return new ResolvedRecipient(UserId: null, Email: options.StaffEmail, Phone: null, OrderNumber: null);
        }

        if (trigger.OrderId is not { } orderId)
        {
            logger.LogWarning(
                "Trigger {TemplateCode} has neither an order nor a staff recipient, notification skipped",
                trigger.TemplateCode);
            return null;
        }

        var orderRecipient = await recipientsRepository.GetOrderRecipientAsync(orderId, cancellationToken);
        if (orderRecipient is null)
        {
            // Заявка заведена до того, как сервис начал слушать OrderCreated, либо это событие
            // ещё не обработано. Ретрай не поможет — уведомление пропускается с предупреждением,
            // как это делает orders-service с событиями о неизвестной ему заявке.
            logger.LogWarning(
                "Unknown order {OrderId} for {TemplateCode}, notification skipped",
                orderId, trigger.TemplateCode);
            return null;
        }

        var recipient = await recipientsRepository.GetRecipientAsync(orderRecipient.RecipientUserId, cancellationToken);
        if (recipient is null)
        {
            // Владелец заявки известен, а его контакты — нет: UserRegistered публикуется только
            // для роли Client, так что сюда попадают заявки, созданные не клиентом (или
            // зарегистрированным до появления этого сервиса).
            logger.LogWarning(
                "No contacts for user {UserId} (order {OrderNumber}), {TemplateCode} skipped",
                orderRecipient.RecipientUserId, orderRecipient.OrderNumber, trigger.TemplateCode);
            return null;
        }

        return new ResolvedRecipient(
            recipient.UserId,
            recipient.Email,
            recipient.Phone,
            orderRecipient.OrderNumber);
    }

    /// <summary>
    /// Номер заявки годится в текст любого уведомления по ней, но в самих событиях его чаще
    /// нет (в <c>PaymentCompleted</c>, <c>DocumentGenerated</c> и событиях груза — только
    /// <c>OrderId</c>). Раз он всё равно поднят вместе с получателем, добавляем его как общий
    /// плейсхолдер — если обработчик события не положил свой.
    /// </summary>
    private static IReadOnlyDictionary<string, string?> WithOrderNumber(
        IReadOnlyDictionary<string, string?> placeholders,
        string? orderNumber)
    {
        if (orderNumber is null || placeholders.ContainsKey("OrderNumber"))
            return placeholders;

        return new Dictionary<string, string?>(placeholders) { ["OrderNumber"] = orderNumber };
    }

    private static string? ContactFor(NotificationChannel channel, ResolvedRecipient recipient) => channel switch
    {
        NotificationChannel.Email => recipient.Email,
        NotificationChannel.Sms => recipient.Phone,
        _ => null,
    };

    private void WarnAboutUnresolvedPlaceholders(
        NotificationTemplate template,
        IReadOnlyDictionary<string, string?> placeholders)
    {
        var unresolved = TemplateRenderer.FindUnresolved(template.Body, placeholders);
        if (unresolved.Count > 0)
        {
            logger.LogWarning(
                "Template {TemplateCode}/{Channel} references unknown placeholders: {Placeholders}",
                template.Code, template.Channel, string.Join(", ", unresolved));
        }
    }

    private sealed record ResolvedRecipient(Guid? UserId, string? Email, string? Phone, string? OrderNumber);
}
