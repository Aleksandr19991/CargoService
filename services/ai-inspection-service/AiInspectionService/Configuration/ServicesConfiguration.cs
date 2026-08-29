using System.Text.Json.Serialization;
using AiInspectionService.API.Filters;
using FluentValidation;
using Mapster;
using MapsterMapper;

namespace AiInspectionService.API.Configuration;

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
        // Scanning explicitly here is what actually wires up MappingRegister's custom .Map() rules.
        TypeAdapterConfig.GlobalSettings.Scan(typeof(Program).Assembly);
        services.AddMapster();

        return services;
    }
}
