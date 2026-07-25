using IdentityService.Domain.Enums;

namespace IdentityService.API.Models.Requests;

public sealed record CreateStaffUserRequest
{
    public required string Name { get; init; }
    public required string LastName { get; init; }
    public required string Phone { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required Role Role { get; init; }
}
