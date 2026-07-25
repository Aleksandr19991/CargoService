namespace IdentityService.API.Models.Requests;

public sealed record RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}
