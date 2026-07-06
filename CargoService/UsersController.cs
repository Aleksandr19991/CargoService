using CargoService.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CargoService;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    [HttpPost("register")]
    public Guid RegisterUser()
    {
        var service = new UsersService();
        var userId = service.RegisterUser();

        return userId;
    }

    // login
    // update
    // change-password
    // delete
    // get all
    // get by id
}
