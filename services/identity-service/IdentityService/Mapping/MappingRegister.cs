using IdentityService.API.Models.Requests;
using IdentityService.API.Models.Responses;
using IdentityService.Application.Models;
using IdentityService.Domain.Entities;
using Mapster;

namespace IdentityService.API.Mapping;

/// <summary>
/// Every request/response DTO ↔ domain entity mapping lives here, not in controllers. Discovered
/// and applied automatically by <c>services.AddMapster()</c> in Program.cs (Mapster scans the
/// assembly for <see cref="IRegister"/> implementations).
/// </summary>
public class MappingRegister : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<User, UserResponse>();
        config.NewConfig<AuthToken, LoginResponse>();

        // RegisterUserRequest has no Role property — User.Role keeps its entity default (Client).
        config.NewConfig<RegisterUserRequest, User>();
        config.NewConfig<CreateStaffUserRequest, User>();
        config.NewConfig<UpdateUserRequest, User>();
    }
}
