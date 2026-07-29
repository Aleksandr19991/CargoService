namespace OrdersService.API.Models.Requests;

public sealed record OrderPartyRequest
{
    public required string City { get; init; }
    public required string OrganizationOrPersonName { get; init; }
    public required bool IsOrganization { get; init; }
    public required string Phone { get; init; }
}
