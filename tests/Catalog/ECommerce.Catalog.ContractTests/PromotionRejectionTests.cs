using ECommerce.Shared.Kernel;
using FluentAssertions;

namespace ECommerce.Catalog.ContractTests;

/// <summary>
/// FR-012, SC-009, PRM-003 [not adopted — see architecture-burndown.md BD-005] — a promotion rejection shows the undiscounted price. The reason is
/// recorded and never shown to the shopper.
/// </summary>
[Collection("pricing")]
public class PromotionRejectionTests(PricingFixture fixture)
{
    [Theory]
    [InlineData("EXPIRED")]
    [InlineData("MINIMUM_ORDER_VALUE_NOT_MET")]
    [InlineData("NOT_ELIGIBLE")]
    public async Task A_rejection_shows_the_undiscounted_price(string reasonCode)
    {
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(priceMinor: 250_000);
        var promotionId = Guid.NewGuid();
        fixture.Promotion.Pricing = id => PromotionFake.Rejected(id, promotionId, reasonCode);

        await using var db = fixture.NewContext();
        var price = await fixture.Resolver(db).ResolveAsync(product.Id, 250_000, "VND");

        price.Current.AmountMinor.Should().Be(250_000L, "the list price stands when nothing applies");
        price.Original.Should().BeNull("there is no discount to strike through");
        price.IsOutOfDate.Should().BeFalse("Promotion answered; the answer is simply 'no discount'");
    }

    [Fact]
    public async Task A_rejection_is_never_a_silent_skip()
    {
        // PRM-003 [not adopted — see architecture-burndown.md BD-005]: the port cannot return "no discount and no reason" — the proto oneof makes
        // that case unrepresentable. This asserts the resolver honours all three arms.
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync();
        fixture.Promotion.Pricing = id => PromotionFake.Rejected(id, Guid.NewGuid(), "EXPIRED");

        await using var db = fixture.NewContext();
        var price = await fixture.Resolver(db).ResolveAsync(product.Id, 250_000, "VND");

        price.Should().NotBeNull();
        fixture.Promotion.GetPricingCallCount.Should().BeGreaterThan(0, "Promotion was consulted");
    }

    [Fact]
    public async Task An_applied_discount_shows_both_prices()
    {
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(priceMinor: 250_000);
        fixture.Promotion.Pricing = id => PromotionFake.Applied(id, Guid.NewGuid(), 180_000);

        await using var db = fixture.NewContext();
        var price = await fixture.Resolver(db).ResolveAsync(product.Id, 250_000, "VND");

        price.Current.AmountMinor.Should().Be(180_000L);
        price.Original!.AmountMinor.Should().Be(250_000L, "FR-010 shows the original struck through");
        price.IsOutOfDate.Should().BeFalse();
    }

    [Fact]
    public async Task A_discount_below_zero_is_never_displayed()
    {
        // FR-016
        await fixture.ResetAsync();
        var product = await fixture.SeedProductAsync(priceMinor: 250_000);
        fixture.Promotion.Pricing = id => PromotionFake.Applied(id, Guid.NewGuid(), -50_000);

        await using var db = fixture.NewContext();
        var price = await fixture.Resolver(db).ResolveAsync(product.Id, 250_000, "VND");

        price.Current.AmountMinor.Should().Be(0L);
    }
}
