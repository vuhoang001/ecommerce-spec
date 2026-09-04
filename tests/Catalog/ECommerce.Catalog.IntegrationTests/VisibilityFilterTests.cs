using System.Net;
using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// FR-001, FR-002, SC-002 — a Hidden or Discontinued product is absent from every read path
/// and indistinguishable from one that never existed.
/// </summary>
[Collection("catalog")]
public class VisibilityFilterTests(CatalogFixture fixture)
{
    [Fact]
    public async Task Hidden_and_discontinued_products_are_absent_from_a_category_listing()
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("Visibility");

        var active = CatalogFixture.NewProduct("Visible one");
        var hidden = CatalogFixture.NewProduct("Hidden one", status: ProductStatus.Hidden);
        var gone = CatalogFixture.NewProduct("Discontinued one", status: ProductStatus.Discontinued);
        var draft = CatalogFixture.NewProduct("Draft one", status: ProductStatus.Draft);

        foreach (var p in new[] { active, hidden, gone, draft }) p.AssignTo(category);

        await fixture.WithDbAsync(async db =>
        {
            db.Add(category);
            db.AddRange(active, hidden, gone, draft);
            await db.SaveChangesAsync();
        });

        var client = fixture.CreateClient();
        var page = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/categories/{category.Id}/products");

        page!.TotalCount.Should().Be(1, "only the Active product is visible to a customer");
        page.Items.Should().ContainSingle().Which.Name.Should().Be("Visible one");
    }

    [Fact]
    public async Task The_global_filter_hides_non_active_products_from_the_data_layer_itself()
    {
        await fixture.ResetAsync();
        await fixture.WithDbAsync(async db =>
        {
            db.Add(CatalogFixture.NewProduct("Hidden", status: ProductStatus.Hidden));
            await db.SaveChangesAsync();
        });

        // The filter fails closed: a query that forgot to mention status still cannot see it.
        var visible = await fixture.WithDbAsync(db => db.Products.CountAsync());
        var everything = await fixture.WithDbAsync(db => db.Products.IgnoreQueryFilters().CountAsync());

        visible.Should().Be(0);
        everything.Should().Be(1, "the row exists; the filter is what keeps it from a customer");
    }
}
