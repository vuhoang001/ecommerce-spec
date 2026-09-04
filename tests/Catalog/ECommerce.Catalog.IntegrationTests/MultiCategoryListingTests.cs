using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>US1/AC3 — FR-006: a product in two categories appears once in each listing.</summary>
[Collection("catalog")]
public class MultiCategoryListingTests(CatalogFixture fixture)
{
    [Fact]
    public async Task A_product_in_two_categories_appears_exactly_once_in_each()
    {
        await fixture.ResetAsync();
        var coffee = CatalogFixture.NewCategory("Coffee");
        var gifts = CatalogFixture.NewCategory("Gifts");

        await fixture.WithDbAsync(async db =>
        {
            db.AddRange(coffee, gifts);
            var product = CatalogFixture.NewProduct("Gift coffee");
            product.AssignTo(coffee);
            product.AssignTo(gifts);
            db.Add(product);
            await db.SaveChangesAsync();
        });

        var client = fixture.CreateClient();
        var inCoffee = await client.GetFromJsonAsync<ProductPageDto>($"/catalog/categories/{coffee.Id}/products");
        var inGifts = await client.GetFromJsonAsync<ProductPageDto>($"/catalog/categories/{gifts.Id}/products");

        inCoffee!.Items.Should().ContainSingle().Which.Name.Should().Be("Gift coffee");
        inGifts!.Items.Should().ContainSingle().Which.Name.Should().Be("Gift coffee");
    }
}
