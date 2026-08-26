using NotificationService.Domain.Enums;

namespace NotificationService.API.Models.Responses;

/// <summary>
/// Запись истории в том виде, в каком её видит клиент. Тело отправленного сообщения не
/// отдаётся: в списке оно только мешает, а показывать письмо целиком — задача отдельного
/// экрана, которого в API §2.6 нет. Причина сбоя отдаётся — клиенту полезно понимать, почему
/// SMS не дошла.
/// </summary>
public sealed record NotificationResponse
{
    public required Guid Id { get; init; }
    public required NotificationChannel Channel { get; init; }
    public required string RecipientContact { get; init; }
    public required string TemplateCode { get; init; }
    public string? Subject { get; init; }
    public required NotificationStatus Status { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? SentAt { get; init; }
    public string? FailureReason { get; init; }
    public RelatedEntityType? RelatedEntityType { get; init; }
    public Guid? RelatedEntityId { get; init; }
}
