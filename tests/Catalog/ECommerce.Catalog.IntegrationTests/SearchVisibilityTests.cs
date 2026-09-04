using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Domain;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>US3/AC3 — FR-018, SC-002: a Hidden product never appears in search results.</summary>
[Collection("catalog")]
public class SearchVisibilityTests(CatalogFixture fixture)
{
    [Fact]
    public async Task A_hidden_product_matching_the_keyword_is_absent()
    {
        await fixture.ResetAsync();
        await fixture.WithDbAsync(async db =>
        {
            db.Add(CatalogFixture.NewProduct("Cà phê visible"));
            db.Add(CatalogFixture.NewProduct("Cà phê hidden", status: ProductStatus.Hidden));
            db.Add(CatalogFixture.NewProduct("Cà phê discontinued", status: ProductStatus.Discontinued));
            await db.SaveChangesAsync();
        });

        var page = await fixture.CreateClient()
            .GetFromJsonAsync<ProductPageDto>("/catalog/products/search?q=ca phe");

        page!.TotalCount.Should().Be(1, "SC-002: zero hidden or discontinued products in any result");
        page.Items.Should().ContainSingle().Which.Name.Should().Be("Cà phê visible");
    }
}
