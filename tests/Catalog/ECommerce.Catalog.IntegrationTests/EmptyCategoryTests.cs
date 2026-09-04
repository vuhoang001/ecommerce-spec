using System.Net;
using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>US1/AC4 — FR-008: an empty category is a 200 with a reason, not an error.</summary>
[Collection("catalog")]
public class EmptyCategoryTests(CatalogFixture fixture)
{
    [Fact]
    public async Task An_empty_category_returns_a_stated_reason_and_not_an_error()
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("Empty");
        await fixture.WithDbAsync(async db => { db.Add(category); await db.SaveChangesAsync(); });

        var client = fixture.CreateClient();
        var response = await client.GetAsync($"/catalog/categories/{category.Id}/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "an empty category is not an error");
        var page = await response.Content.ReadFromJsonAsync<ProductPageDto>();
        page!.Items.Should().BeEmpty();
        page.EmptyReason.Should().Be(ReasonCodes.NoProductsInCategory);
    }

    [Fact]
    public async Task A_category_that_does_not_exist_is_a_stated_rejection()
    {
        await fixture.ResetAsync();
        var client = fixture.CreateClient();
        var response = await client.GetAsync($"/catalog/categories/{Guid.NewGuid()}/products");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(ReasonCodes.CategoryNotFound, "FR-029: every rejection carries a reason");
    }
}
