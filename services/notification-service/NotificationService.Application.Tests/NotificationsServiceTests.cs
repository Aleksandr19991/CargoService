using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Tests;

public class NotificationsServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private const string OrderNumber = "20260827-AB12CD";
    private const string StaffEmail = "warehouse@cargoservice.local";

    private readonly Mock<IRecipientsRepository> recipients = new();
    private readonly Mock<INotificationTemplatesRepository> templates = new();
    private readonly Mock<INotificationLogsRepository> logs = new();
    private readonly Mock<INotificationSenderRegistry> senders = new();

    private readonly List<NotificationMessage> emailsSent = [];
    private readonly List<NotificationMessage> smsSent = [];

    public NotificationsServiceTests()
    {
        recipients
            .Setup(repository => repository.GetOrderRecipientAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderRecipient { OrderId = OrderId, RecipientUserId = UserId, OrderNumber = OrderNumber });

        recipients
            .Setup(repository => repository.GetRecipientAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationRecipient
            {
                UserId = UserId,
                Name = "Иван",
                LastName = "Петров",
                Email = "client@example.com",
                Phone = "+79990000001",
            });

        senders
            .Setup(registry => registry.Resolve(NotificationChannel.Email))
            .Returns(SenderStub(NotificationChannel.Email, emailsSent, NotificationSendResult.Success()));

        senders
            .Setup(registry => registry.Resolve(NotificationChannel.Sms))
            .Returns(SenderStub(NotificationChannel.Sms, smsSent, NotificationSendResult.Success()));
    }

    [Fact]
    public async Task SendAsync_SendsOverEveryChannelWithTemplateAndContact()
    {
        SetupTemplates(EmailTemplate("Статус {{Status}}", "Груз {{TrackingNumber}}: {{Status}}."), SmsTemplate("Груз {{TrackingNumber}}: {{Status}}."));

        var written = CaptureLogs();
        await CreateService().SendAsync(StatusTrigger(), CancellationToken.None);

        Assert.Single(emailsSent);
        Assert.Single(smsSent);
        Assert.Equal(2, written.Count);
        Assert.All(written, log => Assert.Equal(NotificationStatus.Sent, log.Status));
        Assert.All(written, log => Assert.NotNull(log.SentAt));
        Assert.All(written, log => Assert.Equal(UserId, log.RecipientUserId));
    }

    [Fact]
    public async Task SendAsync_WritesFailedLogWhenProviderRejects()
    {
        SetupTemplates(EmailTemplate("Тема", "Тело"));
        senders
            .Setup(registry => registry.Resolve(NotificationChannel.Email))
            .Returns(SenderStub(NotificationChannel.Email, emailsSent, NotificationSendResult.Failure("SMTP: mailbox unavailable")));

        var written = CaptureLogs();
        await CreateService().SendAsync(StatusTrigger(), CancellationToken.None);

        var log = Assert.Single(written);
        Assert.Equal(NotificationStatus.Failed, log.Status);
        Assert.Null(log.SentAt);
        Assert.Equal("SMTP: mailbox unavailable", log.FailureReason);
    }

    [Fact]
    public async Task SendAsync_SkipsChannelDisabledByPreference()
    {
        SetupTemplates(EmailTemplate("Тема", "Тело"), SmsTemplate("Текст"));
        recipients
            .Setup(repository => repository.GetPreferenceAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationPreference { UserId = UserId, EmailEnabled = true, SmsEnabled = false });

        var written = CaptureLogs();
        await CreateService().SendAsync(StatusTrigger(), CancellationToken.None);

        Assert.Single(emailsSent);
        Assert.Empty(smsSent);
        Assert.Single(written);
    }

    [Fact]
    public async Task SendAsync_SendsEverywhereWhenPreferenceWasNeverSaved()
    {
        // Молчание = согласие: клиент, не трогавший настройки, ждёт уведомления о своём грузе.
        SetupTemplates(EmailTemplate("Тема", "Тело"), SmsTemplate("Текст"));
        recipients
            .Setup(repository => repository.GetPreferenceAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);

        await CreateService().SendAsync(StatusTrigger(), CancellationToken.None);

        Assert.Single(emailsSent);
        Assert.Single(smsSent);
    }

    [Fact]
    public async Task SendAsync_SkipsChannelWithoutContact()
    {
        SetupTemplates(EmailTemplate("Тема", "Тело"), SmsTemplate("Текст"));
        recipients
            .Setup(repository => repository.GetRecipientAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationRecipient { UserId = UserId, Email = "client@example.com", Phone = string.Empty });

        await CreateService().SendAsync(StatusTrigger(), CancellationToken.None);

        Assert.Single(emailsSent);
        Assert.Empty(smsSent);
    }

    [Fact]
    public async Task SendAsync_SkipsChannelWithoutRegisteredSender()
    {
        // Шаблон канала есть, интеграции ещё нет (Push) — не ошибка, просто не отправляем.
        SetupTemplates(new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            Code = "CargoStatusChanged",
            Channel = NotificationChannel.Push,
            Body = "Текст",
        });
        senders.Setup(registry => registry.Resolve(NotificationChannel.Push)).Returns((INotificationSender?)null);

        await CreateService().SendAsync(StatusTrigger(), CancellationToken.None);

        logs.Verify(
            repository => repository.AddRangeAsync(It.IsAny<IReadOnlyList<NotificationLog>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_SkipsWhenOrderIsUnknown()
    {
        // Заявку сервис не видел (OrderCreated не приходил) — ретрай не поможет, событие
        // пропускается без записи в историю.
        SetupTemplates(EmailTemplate("Тема", "Тело"));
        recipients
            .Setup(repository => repository.GetOrderRecipientAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderRecipient?)null);

        await CreateService().SendAsync(StatusTrigger(), CancellationToken.None);

        Assert.Empty(emailsSent);
        logs.Verify(
            repository => repository.AddRangeAsync(It.IsAny<IReadOnlyList<NotificationLog>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_SkipsWhenNoTemplatesForCode()
    {
        SetupTemplates();

        await CreateService().SendAsync(StatusTrigger(), CancellationToken.None);

        Assert.Empty(emailsSent);
        recipients.Verify(
            repository => repository.GetOrderRecipientAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_AddsOrderNumberPlaceholderFromMapping()
    {
        // В PaymentCompleted номера заявки нет — его подставляет сам сервис из привязки.
        SetupTemplates(EmailTemplate("Оплата по заявке {{OrderNumber}}", "Заявка {{OrderNumber}} оплачена."));

        await CreateService().SendAsync(
            new NotificationTrigger
            {
                TemplateCode = "PaymentCompleted",
                OrderId = OrderId,
                Placeholders = new Dictionary<string, string?> { ["Amount"] = "1 000,00" },
            },
            CancellationToken.None);

        var message = Assert.Single(emailsSent);
        Assert.Equal($"Оплата по заявке {OrderNumber}", message.Subject);
        Assert.Equal($"Заявка {OrderNumber} оплачена.", message.Body);
    }

    [Fact]
    public async Task SendAsync_UsesConfiguredStaffAddressForStaffTrigger()
    {
        SetupTemplates(EmailTemplate("Повреждение", "Груз {{ShipmentId}}"));

        var written = CaptureLogs();
        await CreateService().SendAsync(StaffTrigger(), CancellationToken.None);

        var message = Assert.Single(emailsSent);
        Assert.Equal(StaffEmail, message.RecipientContact);

        // У служебного уведомления нет пользователя-получателя — в клиентскую историю оно не попадёт.
        var log = Assert.Single(written);
        Assert.Null(log.RecipientUserId);
    }

    [Fact]
    public async Task SendAsync_SkipsStaffTriggerWhenAddressIsNotConfigured()
    {
        SetupTemplates(EmailTemplate("Повреждение", "Груз {{ShipmentId}}"));

        await CreateService(staffEmail: string.Empty).SendAsync(StaffTrigger(), CancellationToken.None);

        Assert.Empty(emailsSent);
    }

    private NotificationsService CreateService(string staffEmail = StaffEmail) =>
        new(recipients.Object,
            templates.Object,
            logs.Object,
            senders.Object,
            new NotificationOptions { StaffEmail = staffEmail },
            NullLogger<NotificationsService>.Instance);

    private void SetupTemplates(params NotificationTemplate[] configured) =>
        templates
            .Setup(repository => repository.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configured);

    private List<NotificationLog> CaptureLogs()
    {
        var written = new List<NotificationLog>();
        logs
            .Setup(repository => repository.AddRangeAsync(It.IsAny<IReadOnlyList<NotificationLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<NotificationLog>, CancellationToken>((entries, _) => written.AddRange(entries))
            .Returns(Task.CompletedTask);

        return written;
    }

    private static INotificationSender SenderStub(
        NotificationChannel channel,
        List<NotificationMessage> captured,
        NotificationSendResult result)
    {
        var sender = new Mock<INotificationSender>();
        sender.SetupGet(instance => instance.Channel).Returns(channel);
        sender
            .Setup(instance => instance.SendAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationMessage, CancellationToken>((message, _) => captured.Add(message))
            .ReturnsAsync(result);

        return sender.Object;
    }

    private static NotificationTemplate EmailTemplate(string subject, string body) => new()
    {
        Id = Guid.NewGuid(),
        Code = "CargoStatusChanged",
        Channel = NotificationChannel.Email,
        Subject = subject,
        Body = body,
    };

    private static NotificationTemplate SmsTemplate(string body) => new()
    {
        Id = Guid.NewGuid(),
        Code = "CargoStatusChanged",
        Channel = NotificationChannel.Sms,
        Body = body,
    };

    private static NotificationTrigger StatusTrigger() => new()
    {
        TemplateCode = "CargoStatusChanged",
        OrderId = OrderId,
        Placeholders = new Dictionary<string, string?>
        {
            ["TrackingNumber"] = "CS-ABC123",
            ["Status"] = "InTransit",
        },
        RelatedEntityType = RelatedEntityType.Shipment,
        RelatedEntityId = Guid.NewGuid(),
    };

    private static NotificationTrigger StaffTrigger() => new()
    {
        TemplateCode = "PackageIntegrityAssessed",
        ToStaff = true,
        Placeholders = new Dictionary<string, string?> { ["ShipmentId"] = Guid.NewGuid().ToString() },
    };
}
