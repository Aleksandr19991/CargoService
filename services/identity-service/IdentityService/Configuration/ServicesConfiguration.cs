using System.Text.Json.Serialization;
using FluentValidation;
using IdentityService.API.Filters;
using Mapster;
using MapsterMapper;

namespace IdentityService.API.Configuration;

public static class ServicesConfiguration
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers(options => options.Filters.AddValidationFilter())
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        services.AddOpenApi();

        // Request DTO validation (FluentValidation) — validators are picked up by ValidationFilter,
        // no per-action wiring needed. Object↔DTO mapping (Mapster) — see Mapping/MappingRegister.cs.
        services.AddValidatorsFromAssemblyContaining<Program>();

        // AddMapster()'s own assembly auto-scan does not reach this assembly in practice (verified
        // empirically — MappingRegister.Register() is never invoked without this explicit call).
        // Every mapping configured here has so far been a trivial 1:1 property-name match that
        // Mapster's zero-config default mapping produces identically, which is why this went
        // unnoticed — but it means MappingRegister's IRegister was never actually wired up.
        TypeAdapterConfig.GlobalSettings.Scan(typeof(Program).Assembly);
        services.AddMapster();

        return services;
    }
}
