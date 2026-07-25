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
        services.AddMapster();

        return services;
    }
}
