using ClientsService.Domain.Enums;

namespace ClientsService.Domain.Entities;

public class Counterparty
{
    public Guid Id { get; set; }
    public Guid ClientAccountId { get; set; }
    public CounterpartyType Type { get; set; }

    // Exactly one of OrganizationName/FullName is populated, depending on Type.
    public string? OrganizationName { get; set; }
    public string? FullName { get; set; }

    public string? Inn { get; set; }
    public string City { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
