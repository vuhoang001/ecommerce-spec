using ECommerce.Catalog.Application.Browse;
using ECommerce.Catalog.Application.Detail;
using ECommerce.Catalog.Application.Filter;
using ECommerce.Catalog.Application.Ports;
using ECommerce.Catalog.Application.Pricing;
using ECommerce.Catalog.Application.Reads;
using ECommerce.Catalog.Infrastructure.Reads;
using ECommerce.Catalog.Infrastructure.Consumers;
using ECommerce.Catalog.Infrastructure.Promotion;
using ECommerce.Catalog.Application.Search;
using ECommerce.Catalog.Infrastructure;
using ECommerce.Host.Endpoints;
using ECommerce.Host.Health;
using ECommerce.Host.Logging;
using ECommerce.Host.RateLimiting;
using ECommerce.Host.Startup;
using ECommerce.Shared.Kernel.Primitives;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddCatalogLogging();                                    // OBS-001

builder.Services.AddSingleton<IClock, SystemClock>();

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Catalog")
            ?? "Host=localhost;Database=ecommerce;Username=ecommerce;Password=ecommerce",
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.Schema)));

// DAT-004: reads execute through Dapper on their own connection; the DbContext is the write
// side only. They share a connection string so the two can never drift onto different databases.
builder.Services.AddScoped<ICatalogReadConnection, CatalogReadConnection>();
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<CatalogDbContext>());
builder.Services.AddScoped<BrowseCategoryQuery>();
builder.Services.AddScoped<GetProductDetailQuery>();
builder.Services.AddSingleton(new CatalogPricingOptions());
builder.Services.AddScoped<IPromotionPricingPort, InProcessPromotionPricingAdapter>();  // COM-001
builder.Services.AddScoped<ProductPriceResolver>();
builder.Services.AddScoped<DiscountChangedHandler>();
builder.Services.AddScoped<DiscountProjectionSeeder>();
builder.Services.AddScoped<SearchProductsQuery>();
builder.Services.AddScoped<FilterProductsQuery>();

builder.Services.AddCatalogRateLimiting(builder.Configuration);  // FR-035
builder.Services.AddCatalogHealthChecks();                       // FR-036
builder.Services.AddProblemDetails();

// UIX-001 / UIX-002 — the frontend is a separate deployable consuming this backend over HTTP,
// so a browser treats every call as cross-origin and blocks it before any code runs. Origins
// come from configuration and default to NONE: an unset value must fail closed rather than
// quietly allow the world.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
{
    if (allowedOrigins.Length == 0)
    {
        // No origin configured: allow nothing. The catalogue still serves same-origin and
        // non-browser callers, so this degrades access rather than availability.
        policy.WithOrigins();
        return;
    }

    policy.WithOrigins(allowedOrigins)
          .WithMethods("GET")          // the catalogue is a read path; nothing else is offered
          .WithHeaders("Content-Type")
          .WithExposedHeaders("Retry-After");   // FR-035: the client must read it to back off
}));

var app = builder.Build();

// DEP-001 — the image brings up its own schema. Without this a fresh database leaves the
// container reporting liveness 200, readiness 503 and every query 500 until a CLI is run from
// outside the image, which is exactly the install-outside-the-image the rule forbids.
await DatabaseMigrator.MigrateAsync(app.Services);

app.UseCatalogLogging();

// Before the rate limiter: a browser preflight that is refused for origin reasons should not
// also consume the caller's token budget.
app.UseCors(CorsPolicyName);

// FR-037: the limiter runs ahead of every handler, so a refused caller never reaches a query
// and no visibility check is ever skipped under load.
app.UseRateLimiter();

app.MapOpenApiDocument();          // UIX-002
app.MapCatalogHealthEndpoints();
app.MapCategoryProducts();
app.MapProductDetail();
app.MapProductSearch();
app.MapProductFilter();

app.Run();

/// <summary>Exposed so WebApplicationFactory can host this application in tests.</summary>
public partial class Program
{
    /// <summary>Named so tests and the host agree on one policy rather than two.</summary>
    public const string CorsPolicyName = "storefront";
}
