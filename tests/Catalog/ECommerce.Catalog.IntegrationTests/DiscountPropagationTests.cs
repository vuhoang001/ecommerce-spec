using ECommerce.Catalog.Domain;
using ECommerce.Catalog.Infrastructure.Consumers;
using ECommerce.Promotion.Contracts.Events;
using ECommerce.Shared.Kernel.Primitives;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// SC-011, FR-031 — a discount starting, changing or ending reaches the discount copy within
/// one minute, so a price range filter never misses a product it should have matched.
/// </summary>
[Collection("catalog")]
public class DiscountPropagationTests(CatalogFixture fixture)
{
    private static DiscountChangedV1 Message(Guid productId, DiscountOutcome outcome,
        long? minor, DateTimeOffset at) => new()
    {
        MessageId = Guid.NewGuid(),
        OccurredAt = at,
        CorrelationId = Guid.NewGuid(),
        CausationId = Guid.NewGuid(),
        ProductId = productId,
        PromotionId = Guid.NewGuid(),
        Outcome = outcome,
        DiscountedPriceMinor = minor
    };

    private async Task<TimeSpan> PropagateAsync(DiscountChangedV1 message)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContextAccessor>().Context;
        var handler = new DiscountChangedHandler(db, new SystemClock());

        var started = DateTimeOffset.UtcNow;
        await handler.HandleAsync(message);
        return DateTimeOffset.UtcNow - started;
    }

    [Fact]
    public async Task A_discount_starting_reaches_the_copy_within_one_minute()
    {
        await fixture.ResetAsync();
        var product = CatalogFixture.NewProduct("Propagating", priceMinor: 250_000);
        await fixture.WithDbAsync(async db => { db.Add(product); await db.SaveChangesAsync(); });

        var elapsed = await PropagateAsync(
            Message(product.Id, DiscountOutcome.Applied, 180_000, DateTimeOffset.UtcNow));

        elapsed.Should().BeLessThan(TimeSpan.FromMinutes(1), "SC-011 gives a one-minute budget");

        var copy = await fixture.WithDbAsync(db =>
            db.DiscountProjections.SingleOrDefaultAsync(d => d.ProductId == product.Id));
        copy.Should().NotBeNull();
        copy!.DiscountedPrice.AmountMinor.Should().Be(180_000L);
    }

    [Fact]
    public async Task A_discount_changing_updates_the_copy()
    {
        await fixture.ResetAsync();
        var product = CatalogFixture.NewProduct("Changing", priceMinor: 250_000);
        await fixture.WithDbAsync(async db => { db.Add(product); await db.SaveChangesAsync(); });

        var t0 = DateTimeOffset.UtcNow;
        await PropagateAsync(Message(product.Id, DiscountOutcome.Applied, 180_000, t0));
        await PropagateAsync(Message(product.Id, DiscountOutcome.Applied, 150_000, t0.AddSeconds(30)));

        var copy = await fixture.WithDbAsync(db =>
            db.DiscountProjections.SingleAsync(d => d.ProductId == product.Id));
        copy.DiscountedPrice.AmountMinor.Should().Be(150_000L);
    }

    [Fact]
    public async Task A_discount_ending_removes_the_copy_so_the_product_matches_on_list_price()
    {
        await fixture.ResetAsync();
        var product = CatalogFixture.NewProduct("Ending", priceMinor: 250_000);
        await fixture.WithDbAsync(async db => { db.Add(product); await db.SaveChangesAsync(); });

        var t0 = DateTimeOffset.UtcNow;
        await PropagateAsync(Message(product.Id, DiscountOutcome.Applied, 180_000, t0));
        await PropagateAsync(Message(product.Id, DiscountOutcome.Withdrawn, null, t0.AddSeconds(30)));

        var count = await fixture.WithDbAsync(db =>
            db.DiscountProjections.CountAsync(d => d.ProductId == product.Id));
        count.Should().Be(0, "FR-027: with no copy the product matches on its original price alone");
    }
}
