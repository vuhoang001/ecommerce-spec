using Microsoft.AspNetCore.Builder;
using Serilog;

namespace ECommerce.Host.Logging;

/// <summary>OBS-001 — structured logging with a correlation identifier on every request.</summary>
public static class LoggingSetup
{
    public static WebApplicationBuilder AddCatalogLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console());
        return builder;
    }

    public static WebApplication UseCatalogLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId",
                    httpContext.TraceIdentifier);
            };
        });
        return app;
    }
}
