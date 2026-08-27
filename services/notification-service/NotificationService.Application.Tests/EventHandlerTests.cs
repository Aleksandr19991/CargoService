using CargoService.Contracts.Events.V1;
using Moq;
using NotificationService.Application.EventHandlers;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Models;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Tests;

public class EventHandlerTests
{
    private readonly Mock<IRecipientsRepository> recipients = new();
    private readonly Mock<INotificationsService> notifications = new();

    [Fact]
    public async Task UserRegistered_StoresContacts()
    {
        var userId = Guid.NewGuid();
        NotificationRecipient? stored = null;
        recipients
            .Setup(repository => repository.UpsertRecipientAsync(It.IsAny<NotificationRecipient>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRecipient, CancellationToken>((recipient, _) => stored = recipient)
            .Returns(Task.CompletedTask);

        await new UserRegisteredHandler(recipients.Object).HandleAsync(
            new UserRegistered
            {
                UserId = userId,
                Name = "Иван",
                LastName = "Петров",
                Phone = "+79990000001",
                Email = "client@example.com",
            },
            CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(userId, stored.UserId);
        Assert.Equal("client@example.com", stored.Email);
        Assert.Equal("+79990000001", stored.Phone);
    }

    [Fact]
    public async Task OrderCreated_StoresOwnerBeforeSending()
    {
        // Порядок здесь и есть суть обработчика: без привязки заявки к владельцу отправлять
        // некуда — включая само это письмо.
        var calls = new List<string>();
        recipients
            .Setup(repository => repository.UpsertOrderRecipientAsync(It.IsAny<OrderRecipient>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("upsert"))
            .Returns(Task.CompletedTask);
        notifications
            .Setup(service => service.SendAsync(It.IsAny<NotificationTrigger>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("send"))
            .Returns(Task.CompletedTask);

        var clientId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await new OrderCreatedHandler(recipients.Object, notifications.Object).HandleAsync(
            new OrderCreated
            {
                OrderId = orderId,
                OrderNumber = "20260827-AB12CD",
                ClientAccountId = clientId,
                OriginCity = "Москва",
                DestinationCity = "Владимир",
            },
            CancellationToken.None);

        Assert.Equal(["upsert", "send"], calls);
        recipients.Verify(
            repository => repository.UpsertOrderRecipientAsync(
                It.Is<OrderRecipient>(mapping =>
                    mapping.OrderId == orderId &&
                    mapping.RecipientUserId == clientId &&
                    mapping.OrderNumber == "20260827-AB12CD"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CargoStatusChanged_PassesTrackingPlaceholders()
    {
        NotificationTrigger? trigger = null;
        notifications
            .Setup(service => service.SendAsync(It.IsAny<NotificationTrigger>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationTrigger, CancellationToken>((value, _) => trigger = value)
            .Returns(Task.CompletedTask);

        var shipmentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await new CargoStatusChangedHandler(notifications.Object).HandleAsync(
            new CargoStatusChanged
            {
                ShipmentId = shipmentId,
                OrderId = orderId,
                TrackingNumber = "CS-ABC123",
                Status = "InTransit",
                Location = "Москва",
            },
            CancellationToken.None);

        Assert.NotNull(trigger);
        Assert.Equal(NotificationTemplateCodes.CargoStatusChanged, trigger.TemplateCode);
        Assert.Equal(orderId, trigger.OrderId);
        Assert.Equal(RelatedEntityType.Shipment, trigger.RelatedEntityType);
        Assert.Equal(shipmentId, trigger.RelatedEntityId);
        Assert.Equal("CS-ABC123", trigger.Placeholders["TrackingNumber"]);
        Assert.Equal("Москва", trigger.Placeholders["Location"]);
    }

    [Fact]
    public async Task PaymentCompleted_FormatsAmountForReader()
    {
        NotificationTrigger? trigger = null;
        notifications
            .Setup(service => service.SendAsync(It.IsAny<NotificationTrigger>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationTrigger, CancellationToken>((value, _) => trigger = value)
            .Returns(Task.CompletedTask);

        await new PaymentCompletedHandler(notifications.Object).HandleAsync(
            new PaymentCompleted { OrderId = Guid.NewGuid(), PaymentId = Guid.NewGuid(), Amount = 12500.5m },
            CancellationToken.None);

        Assert.NotNull(trigger);
        // Разряды в ru-RU разделяет неразрывный пробел (U+00A0), а не обычный — записан здесь
        // явно, чтобы тест ловил смену культуры форматирования, а не повторял её вычисление.
        Assert.Equal("12 500,50", trigger.Placeholders["Amount"]);

        // Номера заявки в событии нет — его подставит NotificationsService из привязки.
        Assert.DoesNotContain("OrderNumber", trigger.Placeholders.Keys);
    }

    [Fact]
    public async Task PackageIntegrityAssessed_NotifiesStaffOnlyWhenDamageFound()
    {
        NotificationTrigger? trigger = null;
        notifications
            .Setup(service => service.SendAsync(It.IsAny<NotificationTrigger>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationTrigger, CancellationToken>((value, _) => trigger = value)
            .Returns(Task.CompletedTask);

        var handler = new PackageIntegrityAssessedHandler(notifications.Object);

        await handler.HandleAsync(
            new PackageIntegrityAssessed
            {
                ShipmentId = Guid.NewGuid(),
                InspectionJobId = Guid.NewGuid(),
                DamageDetected = false,
                Confidence = 0.97,
            },
            CancellationToken.None);

        // «Повреждений нет» на каждое фото превратило бы алерт в фон, который перестают читать.
        Assert.Null(trigger);

        await handler.HandleAsync(
            new PackageIntegrityAssessed
            {
                ShipmentId = Guid.NewGuid(),
                InspectionJobId = Guid.NewGuid(),
                DamageDetected = true,
                Confidence = 0.93,
            },
            CancellationToken.None);

        Assert.NotNull(trigger);
        Assert.True(trigger.ToStaff);
        Assert.Null(trigger.OrderId);
        Assert.Equal("93 %", trigger.Placeholders["Confidence"]);
    }
}
