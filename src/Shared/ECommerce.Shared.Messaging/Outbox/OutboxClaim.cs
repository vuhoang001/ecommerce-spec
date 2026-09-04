namespace ECommerce.Shared.Messaging.Outbox;

/// <summary>
/// REL-002 — the statement the relay uses to claim outbox rows.
/// </summary>
/// <remarks>
/// The constitution names <c>FOR UPDATE SKIP LOCKED</c> specifically, so this repository owns
/// the statement and tests it rather than trusting a library's documented behaviour to stay
/// true across an upgrade (research.md R7). Two concurrent relays running this claim receive
/// disjoint row sets, which is what makes "each row published exactly once" hold.
/// </remarks>
public static class OutboxClaim
{
    /// <summary>
    /// Claims up to <c>@batch</c> undelivered rows, skipping rows another relay holds.
    /// </summary>
    /// <remarks>
    /// The schema is a parameter, not a literal. This is shared infrastructure: each module owns
    /// its own outbox in its own schema (DAT-001), so hardcoding one module's schema here would
    /// both violate DAT-006 and silently point every other module's relay at the wrong table.
    /// </remarks>
    public static string SqlFor(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
            throw new ArgumentException("A relay must name the schema it drains.", nameof(schema));

        return $"""
            SELECT id
            FROM {schema}.outbox_message
            WHERE delivered_at IS NULL
            ORDER BY enqueued_at
            LIMIT @batch
            FOR UPDATE SKIP LOCKED
            """;
    }

    /// <summary>The clause REL-002 requires. Asserted by name so a rewrite cannot quietly drop it.</summary>
    public const string RequiredLockingClause = "FOR UPDATE SKIP LOCKED";
}
