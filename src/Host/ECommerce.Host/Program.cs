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

var app = builder.Build();

app.UseCatalogLogging();

// FR-037: the limiter runs ahead of every handler, so a refused caller never reaches a query
// and no visibility check is ever skipped under load.
app.UseRateLimiter();

app.MapCatalogHealthEndpoints();
app.MapCategoryProducts();
app.MapProductDetail();
app.MapProductSearch();
app.MapProductFilter();

app.Run();

/// <summary>Exposed so WebApplicationFactory can host this application in tests.</summary>
public partial class Program;
