using IdentityService.API.Models.Requests;
using IdentityService.API.Models.Responses;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService;

[Route("api/[controller]")]
[ApiController]
public class UsersController(IUsersService usersService) : ControllerBase
{
    [HttpPost("register")]
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
            Password = request.Password
        };

        var createdUser = await usersService.CreateUserAsync(user, cancellationToken);
        return CreatedAtAction(nameof(GetUserById), new { id = createdUser.Id }, MapToResponse(createdUser));
    }

    [HttpPut("{id}")]
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
    public async Task<IActionResult> DeleteUser([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var deleted = await usersService.DeleteUserAsync(id, cancellationToken);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<List<UserResponse>>> GetAllUsers(CancellationToken cancellationToken)
    {
        var users = await usersService.GetAllUsersAsync(cancellationToken);
        return Ok(users.Select(MapToResponse).ToList());
    }

    [HttpGet("{id}")]
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
        IsDeactivated = user.IsDeactivated
    };
}
