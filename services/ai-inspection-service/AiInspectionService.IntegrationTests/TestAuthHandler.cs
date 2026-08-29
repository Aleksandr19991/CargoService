using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInspectionService.IntegrationTests;

/// <summary>
/// Replaces Keycloak's JwtBearer validation in tests — no real Keycloak is available. A request
/// authenticates as whatever roles are listed (comma-separated) in "X-Test-Roles"; omitting the
/// header means anonymous. "X-Test-User-Id" lands in ClaimTypes.NameIdentifier — сам сервис
/// личность вызывающего не использует (задания заводятся на груз, а не на человека), но
/// заголовок оставлен, чтобы обработчик был одинаковым во всех сервисах.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";
    public const string UserIdHeader = "X-Test-User-Id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out var rolesHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new(ClaimTypes.Name, "test-user") };
        claims.AddRange(rolesHeader
            .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(role => new Claim(ClaimTypes.Role, role)));

        if (Request.Headers.TryGetValue(UserIdHeader, out var userIdHeader))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userIdHeader.ToString()));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
