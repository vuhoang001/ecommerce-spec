using System.Net;
using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Domain;
using ECommerce.Shared.Kernel;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// US4 — FR-021, FR-022, FR-024, FR-025, FR-026, FR-027, FR-028, SC-010.
/// </summary>
[Collection("catalog")]
public class FilterTests(CatalogFixture fixture)
{
    private async Task<(Guid ProductId, Guid CategoryId)> SeedDiscountedAsync(
        long listMinor = 250_000, long? discountedMinor = 180_000, TimeSpan? copyAge = null)
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("Filtered");
        var product = CatalogFixture.NewProduct("Discounted item", priceMinor: listMinor);
        product.AssignTo(category);

        await fixture.WithDbAsync(async db =>
        {
            db.Add(category);
            db.Add(product);
            if (discountedMinor is { } minor)
            {
                var at = DateTimeOffset.UtcNow - (copyAge ?? TimeSpan.Zero);
                db.Add(DiscountProjection.Create(product.Id, Guid.NewGuid(),
                    Money.FromMinor(minor, "VND"), at, at));
            }
            await db.SaveChangesAsync();
        });

        return (product.Id, category.Id);
    }

    [Fact]
    public async Task A_product_matches_on_its_original_price()
    {
        var (productId, _) = await SeedDiscountedAsync();
        var page = await fixture.CreateClient()
            .GetFromJsonAsync<ProductPageDto>("/catalog/products?minPriceMinor=200000&maxPriceMinor=300000");

        page!.Items.Should().ContainSingle().Which.Id.Should().Be(productId);
        page.Items.Single().MatchedOnDiscountedPriceOnly.Should().BeFalse();
    }

    [Fact]
    public async Task A_product_matches_on_its_discounted_price_and_shows_both()
    {
        // 250,000 discounted to 180,000; the range only covers the discounted price.
        var (productId, _) = await SeedDiscountedAsync();
        var page = await fixture.CreateClient()
            .GetFromJsonAsync<ProductPageDto>("/catalog/products?minPriceMinor=150000&maxPriceMinor=200000");

        var item = page!.Items.Should().ContainSingle().Subject;
        item.Id.Should().Be(productId);
        item.MatchedOnDiscountedPriceOnly.Should().BeTrue("FR-028 explains why it appeared");
        item.Price.Current.AmountMinor.Should().Be(180_000L);
        item.Price.Original!.AmountMinor.Should().Be(250_000L);
    }

    [Fact]
    public async Task A_product_is_returned_exactly_once_when_both_prices_match()
    {
        // FR-026: matching on either price still returns the product once.
        var (productId, _) = await SeedDiscountedAsync();
        var page = await fixture.CreateClient()
            .GetFromJsonAsync<ProductPageDto>("/catalog/products?minPriceMinor=100000&maxPriceMinor=300000");

        page!.Items.Count(i => i.Id == productId).Should().Be(1);
        page.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task An_expired_discount_copy_is_excluded_from_the_filter()
    {
        // FR-015 / FR-027: past the staleness limit the product matches on list price alone.
        var (productId, _) = await SeedDiscountedAsync(copyAge: TimeSpan.FromMinutes(20));
        var page = await fixture.CreateClient()
            .GetFromJsonAsync<ProductPageDto>("/catalog/products?minPriceMinor=150000&maxPriceMinor=200000");

        page!.Items.Should().BeEmpty("the 20-minute-old copy is past the 15-minute limit");
        page.EmptyReason.Should().Be(ReasonCodes.NoMatches);
    }

    [Fact]
    public async Task A_product_with_no_copy_matches_on_its_original_price_alone()
    {
        var (productId, _) = await SeedDiscountedAsync(discountedMinor: null);

        var client = fixture.CreateClient();
        var onList = await client.GetFromJsonAsync<ProductPageDto>(
            "/catalog/products?minPriceMinor=200000&maxPriceMinor=300000");
        var onDiscount = await client.GetFromJsonAsync<ProductPageDto>(
            "/catalog/products?minPriceMinor=150000&maxPriceMinor=200000");

        onList!.Items.Should().ContainSingle().Which.Id.Should().Be(productId);
        onDiscount!.Items.Should().BeEmpty("FR-027: no discounted price is known");
    }

    [Fact]
    public async Task Category_and_price_range_combine()
    {
        var (productId, categoryId) = await SeedDiscountedAsync();

        var client = fixture.CreateClient();
        var matching = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/products?categoryId={categoryId}&minPriceMinor=200000&maxPriceMinor=300000");
        var otherCategory = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/products?categoryId={Guid.NewGuid()}&minPriceMinor=200000&maxPriceMinor=300000");

        matching!.Items.Should().ContainSingle().Which.Id.Should().Be(productId);
        otherCategory!.Items.Should().BeEmpty("a returned product satisfies EVERY filter applied");
    }

    [Fact]
    public async Task An_inverted_range_is_an_error_and_never_an_empty_list()
    {
        await SeedDiscountedAsync();
        var response = await fixture.CreateClient()
            .GetAsync("/catalog/products?minPriceMinor=200000&maxPriceMinor=50000");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(ReasonCodes.MinExceedsMax);
        body.Should().NotContain("\"items\"", "FR-022 forbids answering with an empty result");
    }

    [Fact]
    public async Task A_negative_bound_is_rejected()
    {
        await SeedDiscountedAsync();
        var response = await fixture.CreateClient()
            .GetAsync("/catalog/products?minPriceMinor=-1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(ReasonCodes.NegativePriceBound);
    }

    [Fact]
    public async Task An_omitted_bound_is_unbounded_on_that_side()
    {
        var (productId, _) = await SeedDiscountedAsync();
        var page = await fixture.CreateClient()
            .GetFromJsonAsync<ProductPageDto>("/catalog/products?minPriceMinor=100000");

        page!.Items.Should().ContainSingle().Which.Id.Should().Be(productId);
    }

    [Fact]
    public async Task A_hidden_product_never_appears_in_a_filter_result()
    {
        await fixture.ResetAsync();
        await fixture.WithDbAsync(async db =>
        {
            db.Add(CatalogFixture.NewProduct("Hidden", priceMinor: 250_000,
                status: ProductStatus.Hidden));
            await db.SaveChangesAsync();
        });

        var page = await fixture.CreateClient()
            .GetFromJsonAsync<ProductPageDto>("/catalog/products?minPriceMinor=0&maxPriceMinor=999999999");

        page!.Items.Should().BeEmpty("SC-002 holds on every read path, filters included");
    }
}
