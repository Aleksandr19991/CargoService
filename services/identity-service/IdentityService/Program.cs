using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using FluentValidation;
using IdentityService.API.Filters;
using IdentityService.Application.Configuration;
using IdentityService.Domain.Enums;
using IdentityService.Infrastructure.Configuration;
using IdentityService.Persistence;
using IdentityService.Persistence.Configuration;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "IdentityService")
        .WriteTo.Console();

    var seqServerUrl = context.Configuration["Seq:ServerUrl"];
    if (!string.IsNullOrWhiteSpace(seqServerUrl))
        configuration.WriteTo.Seq(seqServerUrl);
});

// Add services to the container.

builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Request DTO validation (FluentValidation) — validators are picked up by ValidationFilter above,
// no per-action wiring needed. Object↔DTO mapping (Mapster) — configured in Mapping/MappingRegister.cs.
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddMapster();

var configuration = builder.Configuration;
var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddPersistence(connectionString);
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(configuration);

var keycloakSection = configuration.GetSection("Keycloak");
var keycloakBaseUrl = keycloakSection["BaseUrl"]
    ?? throw new InvalidOperationException("Keycloak:BaseUrl is not configured.");
var keycloakRealm = keycloakSection["Realm"]
    ?? throw new InvalidOperationException("Keycloak:Realm is not configured.");
var keycloakClientId = keycloakSection["ClientId"]
    ?? throw new InvalidOperationException("Keycloak:ClientId is not configured.");
// The `iss` claim Keycloak stamps into tokens follows KC_HOSTNAME (docker-compose.yml), which is
// intentionally kept stable regardless of the URL used above to actually reach Keycloak over HTTP
// (localhost from the host, http://keycloak:8080 from inside the compose network) — see CLAUDE.md.
var keycloakValidIssuer = keycloakSection["ValidIssuer"]
    ?? throw new InvalidOperationException("Keycloak:ValidIssuer is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MetadataAddress = $"{keycloakBaseUrl}/realms/{keycloakRealm}/.well-known/openid-configuration";
        options.RequireHttpsMetadata = false;
        options.Audience = keycloakClientId;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakValidIssuer,
            ValidateAudience = true,
            ValidateLifetime = true,
        };
        options.Events = new JwtBearerEvents
        {
            // Keycloak puts realm roles in a `realm_access.roles` JSON claim, not individual
            // role claims — map the ones matching our Role enum onto ClaimTypes.Role so that
            // [Authorize(Roles = ...)] works against Keycloak-issued tokens.
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    var realmAccessJson = context.Principal.FindFirst("realm_access")?.Value;
                    if (!string.IsNullOrEmpty(realmAccessJson))
                    {
                        using var document = JsonDocument.Parse(realmAccessJson);
                        if (document.RootElement.TryGetProperty("roles", out var roles))
                        {
                            foreach (var roleElement in roles.EnumerateArray())
                            {
                                var roleName = roleElement.GetString();
                                if (roleName is not null && Enum.TryParse<Role>(roleName, out _))
                                    identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                            }
                        }
                    }
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Makes the top-level-statement-generated Program class visible to
// WebApplicationFactory<Program> in IdentityService.IntegrationTests.
public partial class Program;
