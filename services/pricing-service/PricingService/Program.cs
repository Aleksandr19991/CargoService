using PricingService.API.Configuration;
using PricingService.Application.Configuration;
using PricingService.Infrastructure.Configuration;
using PricingService.Persistence.Configuration;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogLogging();

builder.Services.AddApiServices();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddPersistence(connectionString);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

await app.MigrateDatabaseAsync();

app.MapDevelopmentEndpoints();

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
