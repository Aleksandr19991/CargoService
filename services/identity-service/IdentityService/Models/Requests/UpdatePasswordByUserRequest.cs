namespace IdentityService.API.Models.Requests;

public class UpdatePasswordByUserRequest
{
    public string OldPassword { get; set; }
    public string NewPassword { get; set; }
}
