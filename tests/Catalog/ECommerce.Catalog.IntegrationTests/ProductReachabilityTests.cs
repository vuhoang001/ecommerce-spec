using System.Net;
using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// SC-001 — every Active product is reachable in at most two catalogue requests: one listing or
/// search that returns it, then one detail view. No Active product is reachable only by knowing
/// its identifier in advance.
/// </summary>
[Collection("catalog")]
public class ProductReachabilityTests(CatalogFixture fixture)
{
    [Fact]
    public async Task A_categorised_product_is_reachable_by_listing_then_detail()
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("Reachable");
        var product = CatalogFixture.NewProduct("Findable by browsing");
        product.AssignTo(category);
        await fixture.WithDbAsync(async db =>
        {
            db.Add(category);
            db.Add(product);
            await db.SaveChangesAsync();
        });

        var client = fixture.CreateClient();

        // Request 1 — a listing surfaces it without prior knowledge of its identifier.
        var page = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/categories/{category.Id}/products");
        var found = page!.Items.Should().ContainSingle().Subject;

        // Request 2 — the detail view.
        var detail = await client.GetFromJsonAsync<ProductDetailDto>($"/catalog/products/{found.Id}");
        detail!.Id.Should().Be(product.Id);
    }

    [Fact]
    public async Task An_uncategorised_product_is_still_reachable_by_search_then_detail()
    {
        // A product with no categories is unreachable by browsing but must not be lost.
        await fixture.ResetAsync();
        var product = CatalogFixture.NewProduct("Orphan tea");
        await fixture.WithDbAsync(async db => { db.Add(product); await db.SaveChangesAsync(); });

        var client = fixture.CreateClient();

        var results = await client.GetFromJsonAsync<ProductPageDto>(
            "/catalog/products/search?q=orphan");
        var found = results!.Items.Should().ContainSingle().Subject;

        var detail = await client.GetFromJsonAsync<ProductDetailDto>($"/catalog/products/{found.Id}");
        detail!.Id.Should().Be(product.Id);
    }

    [Fact]
    public async Task Every_active_product_is_surfaced_by_at_least_one_listing_or_search()
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("Coverage");
        var categorised = CatalogFixture.NewProduct("Has a category");
        var uncategorised = CatalogFixture.NewProduct("Has none");
        categorised.AssignTo(category);

        await fixture.WithDbAsync(async db =>
        {
            db.Add(category);
            db.AddRange(categorised, uncategorised);
            await db.SaveChangesAsync();
        });

        var client = fixture.CreateClient();
        var reachable = new HashSet<Guid>();

        var listing = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/categories/{category.Id}/products");
        foreach (var item in listing!.Items) reachable.Add(item.Id);

        var everything = await client.GetFromJsonAsync<ProductPageDto>(
            "/catalog/products?minPriceMinor=0");
        foreach (var item in everything!.Items) reachable.Add(item.Id);

        reachable.Should().Contain([categorised.Id, uncategorised.Id],
            "SC-001: no Active product needs its identifier known in advance");
    }
}
