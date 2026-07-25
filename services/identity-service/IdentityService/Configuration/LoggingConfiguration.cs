using Serilog;

namespace IdentityService.API.Configuration;

public static class LoggingConfiguration
{
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
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

        return builder;
    }
}
