using ECommerce.Catalog.Domain;
using ECommerce.Promotion.Contracts.Events;
using ECommerce.Shared.Kernel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Shared.Messaging.Tests;

/// <summary>
/// REL-004 — handlers must not assume ordering. Delivery in reverse order converges to the same
/// state, because an update applies only when the incoming occurred_at is newer.
/// </summary>
[Collection("messaging")]
public class OutOfOrderDeliveryTests(MessagingFixture fixture)
{
    private static DiscountChangedV1 Applied(Guid productId, long minor, DateTimeOffset at)
        => new()
        {
            MessageId = Guid.NewGuid(),
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
    public async Task Reverse_order_delivery_converges_to_the_newest_fact()
    {
        await fixture.ResetAsync();
        var productId = await SeedProductAsync();
        var t0 = fixture.Clock.UtcNow;

        var older = Applied(productId, 200_000, t0);
        var newer = Applied(productId, 180_000, t0.AddMinutes(5));

        // Deliver newest first, then the older one.
        await using (var db = fixture.NewContext()) await fixture.Handler(db).HandleAsync(newer);
        await using (var db = fixture.NewContext()) await fixture.Handler(db).HandleAsync(older);

        await using var check = fixture.NewContext();
        check.DiscountProjections.Single().DiscountedPrice.AmountMinor
            .Should().Be(180_000L, "the newer fact wins regardless of arrival order");
    }

    [Fact]
    public async Task Forward_order_reaches_the_same_state_as_reverse_order()
    {
        await fixture.ResetAsync();
        var productId = await SeedProductAsync();
        var t0 = fixture.Clock.UtcNow;

        await using (var db = fixture.NewContext())
            await fixture.Handler(db).HandleAsync(Applied(productId, 200_000, t0));
        await using (var db = fixture.NewContext())
            await fixture.Handler(db).HandleAsync(Applied(productId, 180_000, t0.AddMinutes(5)));

        await using var check = fixture.NewContext();
        check.DiscountProjections.Single().DiscountedPrice.AmountMinor.Should().Be(180_000L);
    }

    [Fact]
    public async Task A_late_withdrawal_cannot_undo_a_newer_application()
    {
        await fixture.ResetAsync();
        var productId = await SeedProductAsync();
        var t0 = fixture.Clock.UtcNow;

        await using (var db = fixture.NewContext())
            await fixture.Handler(db).HandleAsync(Applied(productId, 180_000, t0.AddMinutes(5)));

        var lateWithdrawal = new DiscountChangedV1
        {
            MessageId = Guid.NewGuid(),
            OccurredAt = t0,                       // older than the application above
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            ProductId = productId,
            PromotionId = Guid.NewGuid(),
            Outcome = DiscountOutcome.Withdrawn
        };

        await using (var db = fixture.NewContext()) await fixture.Handler(db).HandleAsync(lateWithdrawal);

        await using var check = fixture.NewContext();
        (await check.DiscountProjections.CountAsync()).Should().Be(1,
            "a stale Withdrawn must not delete a newer Applied");
    }

    [Fact]
    public async Task A_current_withdrawal_removes_the_copy()
    {
        await fixture.ResetAsync();
        var productId = await SeedProductAsync();
        var t0 = fixture.Clock.UtcNow;

        await using (var db = fixture.NewContext())
            await fixture.Handler(db).HandleAsync(Applied(productId, 180_000, t0));

        var withdrawal = new DiscountChangedV1
        {
            MessageId = Guid.NewGuid(),
            OccurredAt = t0.AddMinutes(5),
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            ProductId = productId,
            PromotionId = Guid.NewGuid(),
            Outcome = DiscountOutcome.Withdrawn
        };

        await using (var db = fixture.NewContext()) await fixture.Handler(db).HandleAsync(withdrawal);

        await using var check = fixture.NewContext();
        (await check.DiscountProjections.CountAsync()).Should().Be(0,
            "FR-027: with no copy the product matches on its original price alone");
    }
}
