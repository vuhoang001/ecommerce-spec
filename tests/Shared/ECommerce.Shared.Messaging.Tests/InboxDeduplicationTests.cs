using ECommerce.Catalog.Domain;
using ECommerce.Catalog.Infrastructure;
using ECommerce.Promotion.Contracts.Events;
using ECommerce.Shared.Kernel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Shared.Messaging.Tests;

/// <summary>
/// REL-003 — at-least-once delivery deduplicated on (message_id, consumer), with the inbox row
/// written in the same transaction as the business effect. A replay produces one effect.
/// </summary>
[Collection("messaging")]
public class InboxDeduplicationTests(MessagingFixture fixture)
{
    private static DiscountChangedV1 Applied(Guid productId, Guid messageId, long minor, DateTimeOffset at)
        => new()
        {
            MessageId = messageId,
            OccurredAt = at,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            ProductId = productId,
            PromotionId = Guid.NewGuid(),
            Outcome = DiscountOutcome.Applied,
            DiscountedPriceMinor = minor
        };

    private async Task<Guid> SeedProductAsync()
    {
        await using var db = fixture.NewContext();
        var p = Product.Create(Guid.NewGuid(), "P", null, Money.FromMinor(250_000, "VND"), 1,
            ProductStatus.Active);
        db.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task A_replayed_message_produces_exactly_one_effect()
    {
        await fixture.ResetAsync();
        var productId = await SeedProductAsync();
        var messageId = Guid.NewGuid();
        var message = Applied(productId, messageId, 180_000, fixture.Clock.UtcNow);

        await using (var db = fixture.NewContext())
            (await fixture.Handler(db).HandleAsync(message)).Should().BeTrue("first delivery applies");

        await using (var db = fixture.NewContext())
            (await fixture.Handler(db).HandleAsync(message)).Should().BeFalse("a replay is a no-op");

        await using var check = fixture.NewContext();
        (await check.DiscountProjections.CountAsync()).Should().Be(1);
        (await check.InboxMessages.CountAsync()).Should().Be(1, "one inbox row per (message_id, consumer)");
    }

    [Fact]
    public async Task A_duplicate_leaves_state_unchanged_and_raises_no_error()
    {
        await fixture.ResetAsync();
        var productId = await SeedProductAsync();
        var message = Applied(productId, Guid.NewGuid(), 180_000, fixture.Clock.UtcNow);

        await using (var db = fixture.NewContext()) await fixture.Handler(db).HandleAsync(message);

        long PriceOf(CatalogDbContext c) =>
            c.DiscountProjections.Single().DiscountedPrice.AmountMinor;

        await using var db2 = fixture.NewContext();
        var before = PriceOf(db2);

        var act = async () =>
        {
            await using var db3 = fixture.NewContext();
            await fixture.Handler(db3).HandleAsync(message);
        };

        await act.Should().NotThrowAsync("a duplicate is normal under at-least-once delivery");

        await using var db4 = fixture.NewContext();
        PriceOf(db4).Should().Be(before);
    }

    [Fact]
    public async Task The_inbox_row_and_the_effect_share_one_transaction()
    {
        // If the effect fails, no inbox row may survive — otherwise the message is lost for good.
        await fixture.ResetAsync();
        var messageId = Guid.NewGuid();
        var orphan = Applied(Guid.NewGuid(), messageId, 180_000, fixture.Clock.UtcNow);

        await using (var db = fixture.NewContext())
        {
            var act = async () => await fixture.Handler(db).HandleAsync(orphan);
            await act.Should().ThrowAsync<DbUpdateException>("the product does not exist");
        }

        await using var check = fixture.NewContext();
        (await check.InboxMessages.CountAsync()).Should().Be(0,
            "REL-003 puts the inbox row in the same transaction as the effect");
    }
}
