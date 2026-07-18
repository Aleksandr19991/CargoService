using System.Net;
using System.Net.Http.Json;
using IdentityService.API.Models.Requests;
using IdentityService.API.Models.Responses;
using IdentityService.Domain.Enums;
using IdentityService.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.IntegrationTests;

public class UsersControllerTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    [Fact]
    public async Task Register_NewClient_PersistsUserAndEnqueuesOutboxMessage()
    {
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";

        var response = await client.PostAsJsonAsync("api/users/register", new RegisterUserRequest
        {
            Name = "Jane",
            LastName = "Doe",
            Phone = "+1000000",
            Email = email,
            Password = "s3cret",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(body);
        Assert.Equal(Role.Client, body!.Role);
        Assert.False(body.IsDeactivated);

        await using var context = CreateDbContext();
        var storedUser = await context.Users.SingleAsync(u => u.Id == body.Id);
        Assert.Equal(email, storedUser.Email);
        Assert.Equal(Role.Client, storedUser.Role);

        var outboxMessage = await context.OutboxMessages.SingleAsync(m => m.Payload.Contains(email));
        Assert.Equal("identity-service.user-registered", outboxMessage.RoutingKey);
        Assert.Null(outboxMessage.ProcessedAtUtc);
    }

    [Fact]
    public async Task Register_IsAnonymous_NoAuthHeaderRequired()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("api/users/register", new RegisterUserRequest
        {
            Name = "Anon",
            LastName = "User",
            Phone = "+1000000",
            Email = $"{Guid.NewGuid()}@example.com",
            Password = "s3cret",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetAllUsers_NoAuth_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllUsers_ClientRole_ReturnsForbidden()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, nameof(Role.Client));

        var response = await client.GetAsync("api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAllUsers_AdminRole_ReturnsOk()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, nameof(Role.Admin));

        var response = await client.GetAsync("api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateStaffUser_AdminRole_CreatesUserWithoutOutboxMessage()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, nameof(Role.Admin));
        var email = $"{Guid.NewGuid()}@example.com";

        var response = await client.PostAsJsonAsync("api/users/staff", new CreateStaffUserRequest
        {
            Name = "Warehouse",
            LastName = "Op",
            Phone = "+1000000",
            Email = email,
            Password = "s3cret",
            Role = Role.WarehouseOperator,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(Role.WarehouseOperator, body!.Role);

        await using var context = CreateDbContext();
        var outboxMessageExists = await context.OutboxMessages.AnyAsync(m => m.Payload.Contains(email));
        Assert.False(outboxMessageExists);
    }

    [Fact]
    public async Task CreateStaffUser_ClientRole_ReturnsForbidden()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, nameof(Role.Client));

        var response = await client.PostAsJsonAsync("api/users/staff", new CreateStaffUserRequest
        {
            Name = "Should",
            LastName = "Fail",
            Phone = "+1000000",
            Email = $"{Guid.NewGuid()}@example.com",
            Password = "s3cret",
            Role = Role.Admin,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(factory.ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
