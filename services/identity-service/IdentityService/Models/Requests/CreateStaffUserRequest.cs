using IdentityService.Domain.Enums;

namespace IdentityService.API.Models.Requests;

public class CreateStaffUserRequest
{
    public string Name { get; set; }
    public string LastName { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public Role Role { get; set; }
}
