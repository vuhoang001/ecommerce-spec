using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECommerce.Shared.Messaging.Tests;

/// <summary>
/// REL-002 — two concurrent relays claim disjoint rows, so each outbox row is published exactly
/// once. This runs the real statement against real PostgreSQL; the locking semantics are the
/// thing under test, not a mock of them.
/// </summary>
[Collection("messaging")]
public class RelayConcurrencyTests(MessagingFixture fixture)
{
    private async Task SeedOutboxAsync(int rows)
    {
        await using var db = fixture.NewContext();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE catalog.outbox_message;");
        for (var i = 0; i < rows; i++)
        {
            // The payload travels as a parameter: braces in a raw SQL string are treated as
            // format placeholders by ExecuteSqlRawAsync.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO catalog.outbox_message (id, message_type, payload, enqueued_at) " +
                "VALUES (gen_random_uuid(), 'test.event.v1', CAST(@p0 AS jsonb), now());",
                new NpgsqlParameter("p0", "{}"));
        }
    }

    private static async Task<List<Guid>> ClaimAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, int batch)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT id
            FROM catalog.outbox_message
            WHERE delivered_at IS NULL
            ORDER BY enqueued_at
            LIMIT @batch
            FOR UPDATE SKIP LOCKED
            """, connection, transaction);
        command.Parameters.AddWithValue("batch", batch);

        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) ids.Add(reader.GetGuid(0));
        return ids;
    }

    [Fact]
    public async Task Two_concurrent_relays_claim_disjoint_rows()
    {
        await SeedOutboxAsync(10);

        await using var db = fixture.NewContext();
        var connectionString = db.Database.GetConnectionString()!;

        await using var relayA = new NpgsqlConnection(connectionString);
        await using var relayB = new NpgsqlConnection(connectionString);
        await relayA.OpenAsync();
        await relayB.OpenAsync();

        await using var transactionA = await relayA.BeginTransactionAsync();
        var claimedByA = await ClaimAsync(relayA, transactionA, 5);

        // B runs while A still holds its rows. SKIP LOCKED is what stops B from blocking on
        // them AND from taking them.
        await using var transactionB = await relayB.BeginTransactionAsync();
        var claimedByB = await ClaimAsync(relayB, transactionB, 5);

        claimedByA.Should().HaveCount(5);
        claimedByB.Should().HaveCount(5);
        claimedByA.Should().NotIntersectWith(claimedByB,
            "REL-002: each row is claimed by exactly one relay");

        await transactionA.RollbackAsync();
        await transactionB.RollbackAsync();
    }

    [Fact]
    public async Task A_second_relay_is_not_blocked_by_the_first()
    {
        // Without SKIP LOCKED the second relay would wait on the first transaction. Under load
        // that turns two relays into one, which is the failure REL-002 exists to prevent.
        await SeedOutboxAsync(4);

        await using var db = fixture.NewContext();
        var connectionString = db.Database.GetConnectionString()!;

        await using var relayA = new NpgsqlConnection(connectionString);
        await using var relayB = new NpgsqlConnection(connectionString);
        await relayA.OpenAsync();
        await relayB.OpenAsync();

        await using var transactionA = await relayA.BeginTransactionAsync();
        await ClaimAsync(relayA, transactionA, 4);   // A holds every row

        await using var transactionB = await relayB.BeginTransactionAsync();
        var claim = ClaimAsync(relayB, transactionB, 4);

        var finished = await Task.WhenAny(claim, Task.Delay(TimeSpan.FromSeconds(5)));

        finished.Should().Be(claim, "SKIP LOCKED returns immediately instead of waiting");
        (await claim).Should().BeEmpty("every row is held by the other relay");

        await transactionA.RollbackAsync();
        await transactionB.RollbackAsync();
    }
}
