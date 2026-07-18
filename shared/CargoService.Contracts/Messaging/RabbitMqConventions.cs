using System.Text;

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

    /// <summary>Builds the `{publishing-service}.{event-name-in-kebab-case}` routing key for an event published by a service.</summary>
    public static string RoutingKey(string publishingService, string eventTypeName) =>
        $"{publishingService}.{ToKebabCase(eventTypeName)}";

    /// <summary>Builds the `{consuming-service}.{event-name-in-kebab-case}` queue name for a service consuming an event.</summary>
    public static string QueueName(string consumingService, string eventTypeName) =>
        $"{consumingService}.{ToKebabCase(eventTypeName)}";

    private static string ToKebabCase(string pascalCaseName)
    {
        var builder = new StringBuilder(pascalCaseName.Length + 4);
        for (var i = 0; i < pascalCaseName.Length; i++)
        {
            var c = pascalCaseName[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    builder.Append('-');
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
