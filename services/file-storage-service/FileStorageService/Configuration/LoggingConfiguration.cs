using Serilog;

namespace FileStorageService.API.Configuration;

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
                .Enrich.WithProperty("Service", "FileStorageService")
                .WriteTo.Console();

            var seqServerUrl = context.Configuration["Seq:ServerUrl"];
            if (!string.IsNullOrWhiteSpace(seqServerUrl))
                configuration.WriteTo.Seq(seqServerUrl);
        });

        return builder;
    }
}
