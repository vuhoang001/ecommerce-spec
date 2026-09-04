using ECommerce.Catalog.Application.Ports;
using ECommerce.Catalog.Application.Pricing;
using ECommerce.Catalog.Domain;
using ECommerce.Catalog.Infrastructure;
using ECommerce.Shared.Kernel;
using ECommerce.Shared.Kernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace ECommerce.Catalog.ContractTests;

/// <summary>Real PostgreSQL, a controllable Promotion, and a clock the test moves.</summary>
public sealed class PricingFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ecommerce").WithUsername("ecommerce").WithPassword("ecommerce")
        .Build();

    public PromotionFake Promotion { get; } = new();
    public TestClock Clock { get; } = new();
    public CatalogPricingOptions Options { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    public CatalogDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.Schema))
            .Options);

    public async Task ResetAsync()
    {
        await using var db = NewContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE catalog.discount_projection, catalog.inbox_message, " +
            "catalog.product_category, catalog.product_image, catalog.product, catalog.category CASCADE;");
        Promotion.Pricing = null;
        Promotion.Throw = false;
        Promotion.ActiveDiscounts.Clear();
    }

    public ProductPriceResolver Resolver(CatalogDbContext db) =>
        new(db, Promotion, Clock, Options, NullLogger<ProductPriceResolver>.Instance);

    public async Task<Product> SeedProductAsync(long priceMinor = 250_000)
    {
        await using var db = NewContext();
        var product = Product.Create(Guid.NewGuid(), "Cà phê sữa đá", "d",
            Money.FromMinor(priceMinor, "VND"), 5, ProductStatus.Active);
        db.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}

public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
    public void Advance(TimeSpan by) => UtcNow += by;
}

[CollectionDefinition("pricing")]
public sealed class PricingCollection : ICollectionFixture<PricingFixture>;
