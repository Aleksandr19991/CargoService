namespace CargoService.Infrastructure.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public required string HostName { get; init; }
    public required int Port { get; init; }
    public required string UserName { get; init; }
    public required string Password { get; init; }
}
