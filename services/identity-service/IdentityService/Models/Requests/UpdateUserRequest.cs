namespace IdentityService.API.Models.Requests;

public class UpdateUserRequest
{
    public string Name { get; set; }
    public string LastName { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
}