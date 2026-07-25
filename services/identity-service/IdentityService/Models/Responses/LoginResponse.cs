namespace IdentityService.API.Models.Responses;

public sealed record LoginResponse
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required int ExpiresIn { get; init; }
    public required string TokenType { get; init; }
}
