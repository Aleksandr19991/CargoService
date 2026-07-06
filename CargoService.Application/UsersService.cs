using CargoService.Domain.Entities;

namespace CargoService.Application;

public class UsersService
{
    List<User> Users { get; set; } = new List<User>();

    public Guid RegisterUser()
    {
        var userGuid = Guid.NewGuid();
        Users.Add(new() { Id = userGuid });
        //var user = new User()
        //{
        //    Id = userGuid,
        //};
        //Users.Add(user);
        return userGuid;
    }
}

//public static class UsersService
//{
//    static List<User> Users { get; set; } = new List<User>();

//    public static Guid RegisterUser()
//    {
//        var userGuid = Guid.NewGuid();
//        Users.Add(new() { Id = userGuid });
//        //var user = new User()
//        //{
//        //    Id = userGuid,
//        //};
//        //Users.Add(user);
//        return userGuid;
//    }
//}
