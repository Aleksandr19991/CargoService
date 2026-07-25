using IdentityService.Domain.Enums;

namespace IdentityService.API.Models.Requests;

public sealed record ChangeUserRoleRequest
{
    public required Role Role { get; init; }
}
