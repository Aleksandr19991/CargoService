namespace OrdersService.API.Models.Responses;

public sealed record OrderPartyResponse
{
    public required string City { get; init; }
    public required string OrganizationOrPersonName { get; init; }
    public required bool IsOrganization { get; init; }
    public required string Phone { get; init; }
}
