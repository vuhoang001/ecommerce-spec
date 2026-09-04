using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// FR-030, SC-012 — a price survives store, filter-compare and display as the identical
/// integer, with no drift at any hop and no fractional amount anywhere.
/// </summary>
[Collection("catalog")]
public class MoneyRoundTripTests(CatalogFixture fixture)
{
    [Theory]
    [InlineData(1L)]
    [InlineData(50_000L)]
    [InlineData(999_999L)]
    [InlineData(9_007_199_254_740_993L)]   // beyond 2^53: a double would lose this exactly
    public async Task A_price_survives_store_compare_and_display_unchanged(long amountMinor)
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("Money");
        var product = CatalogFixture.NewProduct("Exact", priceMinor: amountMinor);
        product.AssignTo(category);
        await fixture.WithDbAsync(async db =>
        {
            db.Add(category);
            db.Add(product);
            await db.SaveChangesAsync();
        });

        // 1. Stored
        var stored = await fixture.WithDbAsync(db =>
            db.Products.Where(p => p.Id == product.Id)
                .Select(p => EF.Property<long>(p, "_priceMinor")).SingleAsync());
        stored.Should().Be(amountMinor);

        var client = fixture.CreateClient();

        // 2. Compared — an inclusive range of exactly this price must match it
        var filtered = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/products?minPriceMinor={amountMinor}&maxPriceMinor={amountMinor}");
        filtered!.Items.Should().ContainSingle().Which.Id.Should().Be(product.Id,
            "the compared value is the stored value");

        // 3. Displayed
        var detail = await client.GetFromJsonAsync<ProductDetailDto>($"/catalog/products/{product.Id}");
        detail!.Price.Current.AmountMinor.Should().Be(amountMinor, "no drift between hops");
        detail.Price.Current.CurrencyCode.Should().Be("VND");
    }

    [Fact]
    public async Task Every_displayed_amount_is_a_whole_number_on_the_wire()
    {
        await fixture.ResetAsync();
        var product = CatalogFixture.NewProduct("Whole", priceMinor: 50_000);
        await fixture.WithDbAsync(async db => { db.Add(product); await db.SaveChangesAsync(); });

        var json = await fixture.CreateClient()
            .GetStringAsync($"/catalog/products/{product.Id}");

        json.Should().Contain("\"amountMinor\":50000");
        json.Should().NotContain("50000.0", "SC-012: zero fractional amounts anywhere");
    }
}
