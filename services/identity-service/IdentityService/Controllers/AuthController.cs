using IdentityService.API.Models.Requests;
using IdentityService.API.Models.Responses;
using IdentityService.Application.Interfaces;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AuthController(IIdentityProviderClient identityProviderClient, IMapper mapper) : ControllerBase
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

        return Ok(mapper.Map<LoginResponse>(token));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var token = await identityProviderClient.RefreshAsync(request.RefreshToken, cancellationToken);
        if (token is null)
            return Unauthorized();

        return Ok(mapper.Map<LoginResponse>(token));
    }
}
