using ECommerce.Catalog.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace ECommerce.Catalog.ResilienceTests;

/// <summary>
/// Hosts the real application against a real PostgreSQL, with a deliberately tiny rate-limit
/// budget so FR-035's rejection shape can be exercised in a few requests.
/// </summary>
public sealed class ResilienceFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const int TokensPerMinute = 3;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ecommerce").WithUsername("ecommerce").WithPassword("ecommerce")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["RateLimit:TotalTokensPerMinute"] = TokensPerMinute.ToString(),
                ["RateLimit:InstanceCount"] = "1"
            }));

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<CatalogDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<CatalogDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString(), npgsql =>
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.Schema)));
        });

        return base.CreateHost(builder);
    }
}

[CollectionDefinition("resilience")]
public sealed class ResilienceCollection : ICollectionFixture<ResilienceFixture>;
