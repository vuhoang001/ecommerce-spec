using ECommerce.Shared.Messaging.Outbox;
using FluentAssertions;

namespace ECommerce.Shared.Messaging.Tests;

/// <summary>
/// REL-002 — the relay drains the outbox with FOR UPDATE SKIP LOCKED. The constitution names
/// the clause, so it is asserted by name: a rewrite of the claim query cannot quietly drop it.
/// </summary>
public class RelaySqlTests
{
    [Fact]
    public void The_claim_statement_carries_the_locking_clause_the_constitution_names()
    {
        OutboxClaim.SqlFor("catalog").Should().Contain(OutboxClaim.RequiredLockingClause,
            "REL-002 names FOR UPDATE SKIP LOCKED specifically, not merely 'some row lock'");
    }

    [Fact]
    public void The_claim_statement_only_takes_undelivered_rows()
    {
        OutboxClaim.SqlFor("catalog").Should().Contain("delivered_at IS NULL");
    }

    [Fact]
    public void The_claim_statement_targets_the_schema_it_is_given()
    {
        // DAT-006: shared infrastructure must not hardcode one module's schema.
        OutboxClaim.SqlFor("ordering").Should().Contain("FROM ordering.outbox_message");
        OutboxClaim.SqlFor("catalog").Should().NotContain("ordering.");
    }

    [Fact]
    public void The_claim_statement_is_bounded_so_one_relay_cannot_take_the_whole_table()
    {
        OutboxClaim.SqlFor("catalog").Should().Contain("LIMIT");
    }
}
