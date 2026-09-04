using ECommerce.Catalog.Infrastructure;
using ECommerce.Catalog.Infrastructure.Consumers;
using ECommerce.Shared.Kernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ECommerce.Shared.Messaging.Tests;

public sealed class MessagingFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ecommerce").WithUsername("ecommerce").WithPassword("ecommerce")
        .Build();

    public MessagingClock Clock { get; } = new();

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
            "TRUNCATE catalog.discount_projection, catalog.inbox_message, catalog.product CASCADE;");
    }

    public DiscountChangedHandler Handler(CatalogDbContext db) => new(db, Clock);
}

public sealed class MessagingClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
}

[CollectionDefinition("messaging")]
public sealed class MessagingCollection : ICollectionFixture<MessagingFixture>;
