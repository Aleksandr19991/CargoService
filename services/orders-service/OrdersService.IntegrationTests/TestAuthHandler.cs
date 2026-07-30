using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrdersService.IntegrationTests;

/// <summary>
/// Replaces Keycloak's JwtBearer validation in tests — no real Keycloak is available. A request
/// authenticates as whatever roles are listed (comma-separated) in "X-Test-Roles"; omitting the
/// header means anonymous, matching how [Authorize]/[AllowAnonymous] behave for real. Also reads
/// "X-Test-User-Id" into ClaimTypes.NameIdentifier, since OrdersController.GetUserId() relies on
/// the `sub` claim (mapped to NameIdentifier by the real JwtBearer handler) to scope orders to the
/// caller.
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
