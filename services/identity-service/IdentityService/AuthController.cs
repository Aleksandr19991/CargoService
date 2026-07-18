using IdentityService.API.Models.Requests;
using IdentityService.API.Models.Responses;
using IdentityService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AuthController(IIdentityProviderClient identityProviderClient) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var token = await identityProviderClient.AuthenticateAsync(request.Email, request.Password, cancellationToken);
        if (token is null)
            return Unauthorized();

        return Ok(new LoginResponse
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresIn = token.ExpiresIn,
            TokenType = token.TokenType
        });
    }
}
