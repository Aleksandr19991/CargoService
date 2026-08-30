namespace PaymentService.Application.Models;

public record PendingOutboxMessage(Guid Id, string RoutingKey, string Payload);
