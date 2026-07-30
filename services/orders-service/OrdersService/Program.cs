using OrdersService.API.Configuration;
using OrdersService.Application.Configuration;
using OrdersService.Infrastructure.Configuration;
using OrdersService.Persistence.Configuration;
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
// WebApplicationFactory<Program> in OrdersService.IntegrationTests.
public partial class Program;
