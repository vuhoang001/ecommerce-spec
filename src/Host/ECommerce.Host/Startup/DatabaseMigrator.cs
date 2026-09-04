using ECommerce.Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ECommerce.Host.Startup;

/// <summary>
/// DEP-001 — applies pending migrations inside the running image.
/// </summary>
/// <remarks>
/// The rule forbids install steps performed outside the image. Applying the schema with an
/// external CLI is such a step, and skipping it leaves a fresh deployment answering liveness 200,
/// readiness 503 and every query 500 — a failure no integration test can see, because each test
/// suite migrates its own container before the host starts.
/// <para>
/// FR-036 runs several instances, which would otherwise race: EF Core's migration history table
/// is not a mutex, and two instances applying the same migration deadlock or half-apply. A
/// PostgreSQL advisory lock serialises them. It is session-scoped, so the connection is pinned
/// for the lock's lifetime — releasing it to the pool mid-migration would defeat the point, the
/// same trap the discount-copy seeder hit (research.md R12).
/// </para>
/// </remarks>
public static class DatabaseMigrator
{
    /// <summary>Stable and distinct from the seeder's key: different concerns, different locks.</summary>
    private const long AdvisoryLockKey = 0x0CA7A106_5C4E3A00;

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseMigrator));

        await db.Database.OpenConnectionAsync(ct);
        try
        {
            // Blocking, not try-lock: a losing instance must WAIT for the schema, not start
            // serving against a half-migrated database.
            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_lock({AdvisoryLockKey})", ct);

            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("Database schema is current; no migrations to apply.");
                return;
            }

            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync(ct);
            logger.LogInformation("Database schema is now current.");
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({AdvisoryLockKey})", ct);
            await db.Database.CloseConnectionAsync();
        }
    }
}
