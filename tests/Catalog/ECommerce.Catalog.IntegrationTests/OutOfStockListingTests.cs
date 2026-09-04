using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>US1/AC2 — FR-005, SC-007: a stock-0 product stays listed and is labelled.</summary>
[Collection("catalog")]
public class OutOfStockListingTests(CatalogFixture fixture)
{
    [Fact]
    public async Task A_product_with_zero_stock_stays_listed_and_is_flagged()
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("Stock");

        await fixture.WithDbAsync(async db =>
        {
            db.Add(category);
            var inStock = CatalogFixture.NewProduct("In stock", stock: 3);
            var soldOut = CatalogFixture.NewProduct("Sold out", stock: 0);
            inStock.AssignTo(category);
            soldOut.AssignTo(category);
            db.AddRange(inStock, soldOut);
            await db.SaveChangesAsync();
        });

        var client = fixture.CreateClient();
        var page = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/categories/{category.Id}/products");

        page!.TotalCount.Should().Be(2, "stock reaching zero never removes a product (SC-007)");
        page.Items.Single(i => i.Name == "Sold out").IsOutOfStock.Should().BeTrue();
        page.Items.Single(i => i.Name == "In stock").IsOutOfStock.Should().BeFalse();
    }
}
