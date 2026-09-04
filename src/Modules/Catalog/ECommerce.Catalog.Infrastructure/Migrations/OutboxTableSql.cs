namespace ECommerce.Catalog.Infrastructure.Migrations;

/// <summary>
/// The outbox table lives in the catalog schema (DAT-001) — each module owns its own.
/// </summary>
/// <remarks>
/// plan.md Complexity Tracking: this feature registers ZERO publishers. The table and the relay
/// exist so REL-001 and REL-002 are proven before the first module publishes, not because
/// Catalog sends anything.
/// </remarks>
public static class OutboxTableSql
{
    public const string Create = """
        CREATE TABLE IF NOT EXISTS catalog.outbox_message (
            id            uuid        PRIMARY KEY,
            message_type  text        NOT NULL,
            payload       jsonb       NOT NULL,
            enqueued_at   timestamptz NOT NULL,
            delivered_at  timestamptz NULL
        );
        CREATE INDEX IF NOT EXISTS ix_outbox_message_undelivered
            ON catalog.outbox_message (enqueued_at)
            WHERE delivered_at IS NULL;
        """;

    public const string Drop = "DROP TABLE IF EXISTS catalog.outbox_message;";
}
