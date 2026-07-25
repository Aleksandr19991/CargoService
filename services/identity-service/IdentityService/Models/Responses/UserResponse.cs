using IdentityService.Domain.Enums;

namespace IdentityService.API.Models.Responses;

public sealed record UserResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string LastName { get; init; }
    public required string Phone { get; init; }
    public required string Email { get; init; }
    public required Role Role { get; init; }
    public required bool IsDeactivated { get; init; }
}
