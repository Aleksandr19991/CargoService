using IdentityService.API.Configuration;
using IdentityService.Application.Configuration;
using IdentityService.Infrastructure.Configuration;
using IdentityService.Persistence.Configuration;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogLogging();

builder.Services.AddApiServices();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddPersistence(connectionString);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKeycloakAuthentication(builder.Configuration);

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.MapDevelopmentEndpoints();

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Makes the top-level-statement-generated Program class visible to
// WebApplicationFactory<Program> in IdentityService.IntegrationTests.
public partial class Program;
