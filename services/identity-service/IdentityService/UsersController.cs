using IdentityService.API.Models.Requests;
using IdentityService.API.Models.Responses;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UsersController(IUsersService usersService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> RegisterUser(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Name = request.Name,
            LastName = request.LastName,
            Phone = request.Phone,
            Email = request.Email,
            Role = Role.Client
        };

        var createdUser = await usersService.CreateUserAsync(user, request.Password, cancellationToken);
        return CreatedAtAction(nameof(GetUserById), new { id = createdUser.Id }, MapToResponse(createdUser));
    }

    [HttpPost("staff")]
    [Authorize(Roles = nameof(Role.Admin))]
    public async Task<ActionResult<UserResponse>> CreateStaffUser(
        [FromBody] CreateStaffUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Name = request.Name,
            LastName = request.LastName,
            Phone = request.Phone,
            Email = request.Email,
            Role = request.Role
        };

        var createdUser = await usersService.CreateUserAsync(user, request.Password, cancellationToken);
        return CreatedAtAction(nameof(GetUserById), new { id = createdUser.Id }, MapToResponse(createdUser));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{nameof(Role.Admin)},{nameof(Role.Manager)}")]
    public async Task<IActionResult> UpdateUser(
        [FromRoute] Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Name = request.Name,
            LastName = request.LastName,
            Phone = request.Phone,
            Email = request.Email,
            IsDeactivated = false
        };

        var updated = await usersService.UpdateUserAsync(id, user, cancellationToken);
        if (!updated)
            return NotFound();

        return NoContent();
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
        return Ok(users.Select(MapToResponse).ToList());
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{nameof(Role.Admin)},{nameof(Role.Manager)}")]
    public async Task<ActionResult<UserResponse>> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var user = await usersService.GetUserByIdAsync(id, cancellationToken);
        if (user is null)
            return NotFound();

        return Ok(MapToResponse(user));
    }

    private static UserResponse MapToResponse(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        LastName = user.LastName,
        Phone = user.Phone,
        Email = user.Email,
        Role = user.Role,
        IsDeactivated = user.IsDeactivated
    };
}
