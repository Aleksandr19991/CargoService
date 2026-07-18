namespace IdentityService.Application.Models;

public record AuthToken(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    int RefreshExpiresIn,
    string TokenType);
