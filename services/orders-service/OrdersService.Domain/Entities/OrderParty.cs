namespace OrdersService.Domain.Entities;

/// <summary>Shared shape for Order.Sender/Order.Recipient (see spec.md §2.4) — an EF Core owned type, not its own table.</summary>
public class OrderParty
{
    public string City { get; set; } = string.Empty;

    // Holds either the organization's name or the individual's full name, depending on IsOrganization.
    public string OrganizationOrPersonName { get; set; } = string.Empty;
    public bool IsOrganization { get; set; }
    public string Phone { get; set; } = string.Empty;
}
