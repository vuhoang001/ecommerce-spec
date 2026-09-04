using ECommerce.Catalog.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ECommerce.Host.Health;

/// <summary>
/// FR-036 — liveness and readiness for a redundant deployment.
/// </summary>
/// <remarks>
/// research.md R13: readiness checks the database and migrations, and deliberately does NOT
/// check the Promotion module. Including it would mark every instance unready during a
/// Promotion outage, the load balancer would drain them all, and a degraded dependency would
/// become a total catalogue outage — precisely what SC-008 exists to prevent.
/// </remarks>
public static class HealthEndpoints
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddCatalogHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<CatalogDbContext>("catalog-database", tags: [ReadyTag]);
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            // Liveness answers "is this process responsive", nothing more.
            Predicate = _ => false
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag)
        });

        return app;
    }
}
