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
    /// <summary>Claims up to <c>@batch</c> undelivered rows, skipping rows another relay holds.</summary>
    public const string Sql = """
        SELECT id
        FROM catalog.outbox_message
        WHERE delivered_at IS NULL
        ORDER BY enqueued_at
        LIMIT @batch
        FOR UPDATE SKIP LOCKED
        """;

    /// <summary>The clause REL-002 requires. Asserted by name so a rewrite cannot quietly drop it.</summary>
    public const string RequiredLockingClause = "FOR UPDATE SKIP LOCKED";
}
