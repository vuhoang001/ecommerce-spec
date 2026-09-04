using ECommerce.Catalog.Domain;
using ECommerce.Shared.Kernel;
using FluentAssertions;

namespace ECommerce.Catalog.ContractTests;

/// <summary>
/// FR-015 — a copy past the staleness limit, and an absent copy, both fall back to the
/// undiscounted price marked possibly out of date.
/// </summary>
[Collection("pricing")]
public class ProjectionStalenessTests(PricingFixture fixture)
{
    [Fact]
    public async Task A_copy_older_than_the_staleness_limit_is_not_shown()
    {
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(250_000);

        await using (var seed = fixture.NewContext())
        {
            seed.Add(DiscountProjection.Create(product.Id, Guid.NewGuid(),
                Money.FromMinor(180_000, "VND"), fixture.Clock.UtcNow, fixture.Clock.UtcNow));
            await seed.SaveChangesAsync();
        }

        fixture.Promotion.Pricing = id => PromotionFake.Unavailable(id);
        fixture.Clock.Advance(TimeSpan.FromMinutes(16));   // past the 15-minute limit

        await using var db = fixture.NewContext();
        var price = await fixture.Resolver(db).ResolveAsync(product.Id, 250_000, "VND");

        price.Current.AmountMinor.Should().Be(250_000L,
            "past the limit the catalogue shows a price it can still stand behind");
        price.Original.Should().BeNull();
        price.IsOutOfDate.Should().BeTrue("Promotion could not be consulted");
    }

    [Fact]
    public async Task A_copy_inside_the_limit_is_shown()
    {
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(250_000);

        await using (var seed = fixture.NewContext())
        {
            seed.Add(DiscountProjection.Create(product.Id, Guid.NewGuid(),
                Money.FromMinor(180_000, "VND"), fixture.Clock.UtcNow, fixture.Clock.UtcNow));
            await seed.SaveChangesAsync();
        }

        fixture.Promotion.Pricing = id => PromotionFake.Unavailable(id);
        fixture.Clock.Advance(TimeSpan.FromMinutes(14));

        await using var db = fixture.NewContext();
        var price = await fixture.Resolver(db).ResolveAsync(product.Id, 250_000, "VND");

        price.Current.AmountMinor.Should().Be(180_000L);
    }

    [Fact]
    public async Task No_copy_at_all_falls_back_to_the_list_price_marked_out_of_date()
    {
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(250_000);
        fixture.Promotion.Pricing = id => PromotionFake.Unavailable(id);

        await using var db = fixture.NewContext();
        var price = await fixture.Resolver(db).ResolveAsync(product.Id, 250_000, "VND");

        price.Current.AmountMinor.Should().Be(250_000L);
        price.IsOutOfDate.Should().BeTrue("nothing stale exists to fall back to");
    }
}
