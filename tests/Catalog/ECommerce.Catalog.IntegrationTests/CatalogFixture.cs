using ECommerce.Catalog.Domain;
using ECommerce.Shared.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// A real PostgreSQL 16 in a container plus the real host. Nothing here is mocked, so the
/// unaccent/pg_trgm behaviour, the global query filter and the migrations are all exercised
/// as they will run in production (research.md R10).
/// </summary>
public sealed class CatalogFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ecommerce")
        .WithUsername("ecommerce")
        .WithPassword("ecommerce")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContextAccessor>().Context;
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<Catalog.Infrastructure.CatalogDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<Catalog.Infrastructure.CatalogDbContext>(options =>
                options.UseNpgsql(ConnectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__ef_migrations_history",
                        Catalog.Infrastructure.CatalogDbContext.Schema)));

            services.AddScoped<CatalogDbContextAccessor>();
        });
        return base.CreateHost(builder);
    }

    public async Task ResetAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContextAccessor>().Context;
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE catalog.product_category, catalog.product_image, catalog.product, catalog.category CASCADE;");
    }

    public async Task<T> WithDbAsync<T>(Func<Catalog.Infrastructure.CatalogDbContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContextAccessor>().Context;
        return await work(db);
    }

    public Task WithDbAsync(Func<Catalog.Infrastructure.CatalogDbContext, Task> work)
        => WithDbAsync(async db => { await work(db); return 0; });

    public static Product NewProduct(
        string name = "Cà phê sữa đá",
        long priceMinor = 50_000,
        int stock = 5,
        ProductStatus status = ProductStatus.Active,
        DateTimeOffset? createdAt = null)
        => Product.Create(Guid.NewGuid(), name, "A drink.", Money.FromMinor(priceMinor, "VND"),
            stock, status, createdAt ?? TestClock.Now);

    /// <summary>ARC-004: tests inject time too, so seeded ordering is deterministic.</summary>
    public static class TestClock
    {
        public static DateTimeOffset Now { get; } = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
    }

    public static Category NewCategory(string name = "Coffee")
        => Category.Create(Guid.NewGuid(), name, name.ToLowerInvariant().Replace(' ', '-'));
}

/// <summary>Resolves the module DbContext without leaking EF types into every test signature.</summary>
public sealed class CatalogDbContextAccessor(Catalog.Infrastructure.CatalogDbContext context)
{
    public Catalog.Infrastructure.CatalogDbContext Context { get; } = context;
}

[CollectionDefinition("catalog")]
public sealed class CatalogCollection : ICollectionFixture<CatalogFixture>;
