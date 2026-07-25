using IdentityService.API.Models.Requests;
using IdentityService.API.Models.Responses;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UsersController(IUsersService usersService, IMapper mapper) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> RegisterUser(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = mapper.Map<User>(request);
        var createdUser = await usersService.CreateUserAsync(user, request.Password, cancellationToken);
        return CreatedAtAction(nameof(GetUserById), new { id = createdUser.Id }, mapper.Map<UserResponse>(createdUser));
    }

    [HttpPost("staff")]
    [Authorize(Roles = nameof(Role.Admin))]
    public async Task<ActionResult<UserResponse>> CreateStaffUser(
        [FromBody] CreateStaffUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = mapper.Map<User>(request);
        var createdUser = await usersService.CreateUserAsync(user, request.Password, cancellationToken);
        return CreatedAtAction(nameof(GetUserById), new { id = createdUser.Id }, mapper.Map<UserResponse>(createdUser));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{nameof(Role.Admin)},{nameof(Role.Manager)}")]
    public async Task<IActionResult> UpdateUser(
        [FromRoute] Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = mapper.Map<User>(request);
        var updated = await usersService.UpdateUserAsync(id, user, cancellationToken);
        if (!updated)
            return NotFound();

        return NoContent();
    }

    [HttpPut("{id}/role")]
    [Authorize(Roles = $"{nameof(Role.Admin)},{nameof(Role.Manager)}")]
    public async Task<ActionResult<UserResponse>> ChangeUserRole(
        [FromRoute] Guid id,
        [FromBody] ChangeUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var updatedUser = await usersService.ChangeUserRoleAsync(id, request.Role, cancellationToken);
        if (updatedUser is null)
            return NotFound();

        return Ok(mapper.Map<UserResponse>(updatedUser));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = nameof(Role.Admin))]
    public async Task<IActionResult> DeleteUser([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var deleted = await usersService.DeleteUserAsync(id, cancellationToken);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpGet]
    [Authorize(Roles = $"{nameof(Role.Admin)},{nameof(Role.Manager)}")]
    public async Task<ActionResult<List<UserResponse>>> GetAllUsers(CancellationToken cancellationToken)
    {
        var users = await usersService.GetAllUsersAsync(cancellationToken);
        return Ok(mapper.Map<List<UserResponse>>(users));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{nameof(Role.Admin)},{nameof(Role.Manager)}")]
    public async Task<ActionResult<UserResponse>> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await usersService.GetUserByIdAsync(id, cancellationToken);
        if (user is null)
            return NotFound();

        return Ok(mapper.Map<UserResponse>(user));
    }
}
