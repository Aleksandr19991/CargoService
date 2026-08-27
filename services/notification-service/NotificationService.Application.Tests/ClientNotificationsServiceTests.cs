using Moq;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Tests;

public class ClientNotificationsServiceTests
{
    private readonly Mock<INotificationLogsRepository> logs = new();
    private readonly Mock<IRecipientsRepository> recipients = new();

    [Fact]
    public async Task GetPreferencesAsync_ReturnsEverythingEnabledWhenNeverSaved()
    {
        var userId = Guid.NewGuid();
        recipients
            .Setup(repository => repository.GetPreferenceAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationPreference?)null);

        var preferences = await CreateService().GetPreferencesAsync(userId, CancellationToken.None);

        // Кабинету всегда есть что показать: «не сохраняли» — это не 404, а умолчание.
        Assert.Equal(userId, preferences.UserId);
        Assert.True(preferences.EmailEnabled);
        Assert.True(preferences.SmsEnabled);
    }

    [Fact]
    public async Task GetPreferencesAsync_ReturnsSavedValues()
    {
        var userId = Guid.NewGuid();
        recipients
            .Setup(repository => repository.GetPreferenceAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationPreference { UserId = userId, EmailEnabled = false, SmsEnabled = true });

        var preferences = await CreateService().GetPreferencesAsync(userId, CancellationToken.None);

        Assert.False(preferences.EmailEnabled);
        Assert.True(preferences.SmsEnabled);
    }

    [Fact]
    public async Task GetHistoryAsync_PassesFilterAndPagingThrough()
    {
        var userId = Guid.NewGuid();
        logs
            .Setup(repository => repository.GetByRecipientAsync(
                userId, NotificationChannel.Sms, 2, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<NotificationLog>)[new NotificationLog()], 7));

        var (items, totalCount) = await CreateService()
            .GetHistoryAsync(userId, NotificationChannel.Sms, 2, 5, CancellationToken.None);

        Assert.Single(items);
        Assert.Equal(7, totalCount);
    }

    private ClientNotificationsService CreateService() => new(logs.Object, recipients.Object);
}
