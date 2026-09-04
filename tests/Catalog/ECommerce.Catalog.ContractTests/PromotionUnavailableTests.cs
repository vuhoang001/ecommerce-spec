using ECommerce.Catalog.Domain;
using ECommerce.Shared.Kernel;
using FluentAssertions;

namespace ECommerce.Catalog.ContractTests;

/// <summary>
/// FR-013, SC-008 — an unreachable Promotion falls back to the discount copy, marked possibly
/// out of date, and the page still renders.
/// </summary>
[Collection("pricing")]
public class PromotionUnavailableTests(PricingFixture fixture)
{
    private async Task SeedCopyAsync(Guid productId, long discounted, TimeSpan age)
    {
        await using var db = fixture.NewContext();
        db.Add(DiscountProjection.Create(productId, Guid.NewGuid(),
            Money.FromMinor(discounted, "VND"),
            fixture.Clock.UtcNow - age, fixture.Clock.UtcNow - age));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task An_unavailable_promotion_falls_back_to_a_fresh_copy_marked_out_of_date()
    {
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(250_000);
        await SeedCopyAsync(product.Id, 180_000, TimeSpan.FromMinutes(5));
        fixture.Promotion.Pricing = id => PromotionFake.Unavailable(id);

        await using var db = fixture.NewContext();
        var price = await fixture.Resolver(db).ResolveAsync(product.Id, 250_000, "VND");

        price.Current.AmountMinor.Should().Be(180_000L, "the copy is 5 minutes old and usable");
        price.Original!.AmountMinor.Should().Be(250_000L);
        price.IsOutOfDate.Should().BeTrue("FR-013 marks a price the catalogue cannot re-confirm");
    }

    [Fact]
    public async Task A_thrown_exception_is_treated_as_unreachable_and_still_renders()
    {
        // SC-008: every listing and detail view renders while Promotion is down.
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(250_000);
        await SeedCopyAsync(product.Id, 180_000, TimeSpan.FromMinutes(1));
        fixture.Promotion.Throw = true;

        await using var db = fixture.NewContext();
        var act = async () => await fixture.Resolver(db).ResolveAsync(product.Id, 250_000, "VND");

        var price = await act.Should().NotThrowAsync();
        price.Subject.Current.AmountMinor.Should().Be(180_000L);
        price.Subject.IsOutOfDate.Should().BeTrue();
    }
}
