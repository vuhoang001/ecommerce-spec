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

    /// <summary>How long to wait for a database that is starting alongside this process.</summary>
    private static readonly TimeSpan ConnectRetryWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(3);

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseMigrator));

        // An orchestrator starts this process and its database together, so "not reachable yet"
        // is an ordinary condition rather than a failure. Retry for a bounded window, then give
        // up loudly: a database that is genuinely misconfigured must not be waited on forever,
        // and crash-looping is the orchestrator's signal that something needs attention.
        await WaitForDatabaseAsync(db, logger, ct);

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

    private static async Task WaitForDatabaseAsync(
        CatalogDbContext db, ILogger logger, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + ConnectRetryWindow;
        var attempt = 0;

        while (true)
        {
            attempt++;
            string reason;
            try
            {
                // CanConnectAsync returns false for most connection failures rather than
                // throwing, so relying on a catch block alone logs nothing and leaves an
                // operator watching a silent container.
                if (await db.Database.CanConnectAsync(ct)) return;
                reason = "the server did not accept a connection";
            }
            catch (Exception ex) when (DateTimeOffset.UtcNow < deadline)
            {
                reason = ex.Message;
            }

            logger.LogInformation(
                "Database not reachable yet (attempt {Attempt}): {Reason}. Retrying for up to {Window}s.",
                attempt, reason, ConnectRetryWindow.TotalSeconds);

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new InvalidOperationException(
                    $"The database was not reachable within {ConnectRetryWindow.TotalSeconds:N0}s " +
                    $"after {attempt} attempt(s). Migrations cannot be applied, so this instance " +
                    "would serve against a schema-less database (DEP-001).");
            }

            await Task.Delay(ConnectRetryDelay, ct);
        }
    }
}
