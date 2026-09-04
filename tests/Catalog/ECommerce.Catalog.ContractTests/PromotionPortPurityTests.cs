using ECommerce.Catalog.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.ContractTests;

/// <summary>
/// PRM-001 [not adopted — see architecture-burndown.md BD-005] (NON-NEGOTIABLE), FR-011 — the promotion module calculates and RETURNS a result; the
/// caller applies it. Calling the port twice with the same input yields the same result and
/// changes no state.
/// </summary>
[Collection("pricing")]
public class PromotionPortPurityTests(PricingFixture fixture)
{
    [Fact]
    public async Task Calling_the_port_twice_with_the_same_input_yields_the_same_result()
    {
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(250_000);
        var promotionId = Guid.NewGuid();
        fixture.Promotion.Pricing = id => PromotionFake.Applied(id, promotionId, 180_000);

        var first = await fixture.Promotion.GetPricingAsync(product.Id, 250_000, "VND");
        var second = await fixture.Promotion.GetPricingAsync(product.Id, 250_000, "VND");

        second.Should().BeEquivalentTo(first, "a pricing read is a pure function of its input");
    }

    [Fact]
    public async Task Calling_the_port_changes_no_catalog_state()
    {
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(250_000);
        fixture.Promotion.Pricing = id => PromotionFake.Applied(id, Guid.NewGuid(), 180_000);

        async Task<(int Products, int Copies)> SnapshotAsync()
        {
            await using var db = fixture.NewContext();
            return (await db.Products.CountAsync(), await db.DiscountProjections.CountAsync());
        }

        var before = await SnapshotAsync();
        await fixture.Promotion.GetPricingAsync(product.Id, 250_000, "VND");
        await fixture.Promotion.GetPricingAsync(product.Id, 250_000, "VND");
        var after = await SnapshotAsync();

        after.Should().Be(before, "PRM-001 [not adopted — see architecture-burndown.md BD-005]: promotion never mutates order or catalog data");
    }

    [Fact]
    public async Task Resolving_a_price_writes_nothing()
    {
        // The read path is read-only end to end (TXN-001 is vacuous here for that reason).
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(250_000);
        fixture.Promotion.Pricing = id => PromotionFake.Applied(id, Guid.NewGuid(), 180_000);

        await using var db = fixture.NewContext();
        await fixture.Resolver(db).ResolveAsync(product.Id, 250_000, "VND");

        db.ChangeTracker.HasChanges().Should().BeFalse("resolving a price is a read");
    }
}
