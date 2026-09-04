using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Domain;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>FR-004 — each listed product carries name, primary image and current price.</summary>
[Collection("catalog")]
public class ListingFieldsTests(CatalogFixture fixture)
{
    [Fact]
    public async Task Each_listed_product_shows_its_name_primary_image_and_price()
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("Fields");

        var product = CatalogFixture.NewProduct("Trà đá", priceMinor: 12_000);
        product.AssignTo(category);
        product.AddImage(ProductImage.Create(Guid.NewGuid(), product.Id, "https://img/second.png", 1, false));
        product.AddImage(ProductImage.Create(Guid.NewGuid(), product.Id, "https://img/primary.png", 0, true));

        await fixture.WithDbAsync(async db =>
        {
            db.Add(category);
            db.Add(product);
            await db.SaveChangesAsync();
        });

        var client = fixture.CreateClient();
        var page = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/categories/{category.Id}/products");

        var item = page!.Items.Should().ContainSingle().Subject;
        item.Name.Should().Be("Trà đá");
        item.PrimaryImageUrl.Should().Be("https://img/primary.png");
        item.Price.Current.AmountMinor.Should().Be(12_000L);
        item.Price.Current.CurrencyCode.Should().Be("VND");
    }

    [Fact]
    public async Task A_product_with_no_images_still_lists()
    {
        await fixture.ResetAsync();
        var category = CatalogFixture.NewCategory("NoImages");
        await fixture.WithDbAsync(async db =>
        {
            db.Add(category);
            var p = CatalogFixture.NewProduct("Imageless");
            p.AssignTo(category);
            db.Add(p);
            await db.SaveChangesAsync();
        });

        var client = fixture.CreateClient();
        var page = await client.GetFromJsonAsync<ProductPageDto>(
            $"/catalog/categories/{category.Id}/products");

        page!.Items.Should().ContainSingle().Which.PrimaryImageUrl.Should().BeNull();
    }
}
