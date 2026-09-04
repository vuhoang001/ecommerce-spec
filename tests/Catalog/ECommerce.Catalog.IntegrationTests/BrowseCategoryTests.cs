using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>US1/AC1 — FR-003, FR-007: a page of a category, with the total and position stated.</summary>
[Collection("catalog")]
public class BrowseCategoryTests(CatalogFixture fixture)
{
    [Fact]
    public async Task Shows_the_first_page_of_a_thirty_product_category_with_total_and_position()
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("Paging");

        await fixture.WithDbAsync(async db =>
        {
            db.Add(category);
            for (var i = 0; i < 30; i++)
            {
                var p = CatalogFixture.NewProduct($"Product {i:D2}",
                    createdAt: DateTimeOffset.UtcNow.AddMinutes(-i));
                p.AssignTo(category);
                db.Add(p);
            }
            await db.SaveChangesAsync();
        });

        var client = fixture.CreateClient();
        var page = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/categories/{category.Id}/products");

        page!.Items.Should().HaveCount(24, "the default page size is 24");
        page.TotalCount.Should().Be(30);
        page.Page.Should().Be(1);
        page.PageSize.Should().Be(24);
        page.EmptyReason.Should().BeNull();
    }

    [Fact]
    public async Task Second_page_holds_the_remainder_and_never_repeats_a_row()
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("Paging2");

        await fixture.WithDbAsync(async db =>
        {
            db.Add(category);
            for (var i = 0; i < 30; i++)
            {
                var p = CatalogFixture.NewProduct($"Product {i:D2}",
                    createdAt: DateTimeOffset.UtcNow.AddMinutes(-i));
                p.AssignTo(category);
                db.Add(p);
            }
            await db.SaveChangesAsync();
        });

        var client = fixture.CreateClient();
        var first = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/categories/{category.Id}/products?page=1");
        var second = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/categories/{category.Id}/products?page=2");

        second!.Items.Should().HaveCount(6);
        second.Page.Should().Be(2);
        second.Items.Select(i => i.Id).Should().NotIntersectWith(first!.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task A_page_beyond_the_last_is_empty_and_says_so()
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("Beyond");
        await fixture.WithDbAsync(async db =>
        {
            db.Add(category);
            var p = CatalogFixture.NewProduct();
            p.AssignTo(category);
            db.Add(p);
            await db.SaveChangesAsync();
        });

        var client = fixture.CreateClient();
        var page = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/categories/{category.Id}/products?page=99");

        page!.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(1);
        page.EmptyReason.Should().Be(ReasonCodes.PageBeyondLast);
    }
}
