namespace CargoService.Contracts.Messaging;

/// <summary>
/// Naming conventions for RabbitMQ topology shared by every service. See
/// Events/README.md for the full rationale and worked examples.
/// </summary>
public static class RabbitMqConventions
{
    /// <summary>Single topic exchange all integration events are published to.</summary>
    public const string EventsExchange = "cargoservice.events";

    /// <summary>Suffix appended to a queue name to get its dead-letter queue name.</summary>
    public const string DeadLetterSuffix = ".dlq";
}
