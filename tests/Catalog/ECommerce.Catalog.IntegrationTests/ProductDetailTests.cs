using System.Net;
using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Domain;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>US2/AC1 — FR-009: everything a customer needs to decide.</summary>
[Collection("catalog")]
public class ProductDetailTests(CatalogFixture fixture)
{
    [Fact]
    public async Task Shows_name_description_price_gallery_stock_and_every_category()
    {
        await fixture.ResetAsync();
        var coffee = CatalogFixture.NewCategory("Coffee");
        var gifts = CatalogFixture.NewCategory("Gifts");
        var product = CatalogFixture.NewProduct("Cà phê sữa đá", priceMinor: 50_000, stock: 7);
        product.AssignTo(coffee);
        product.AssignTo(gifts);
        product.AddImage(ProductImage.Create(Guid.NewGuid(), product.Id, "https://img/a.png", 0, true));
        product.AddImage(ProductImage.Create(Guid.NewGuid(), product.Id, "https://img/b.png", 1, false));

        await fixture.WithDbAsync(async db =>
        {
            db.AddRange(coffee, gifts);
            db.Add(product);
            await db.SaveChangesAsync();
        });

        var detail = await fixture.CreateClient()
            .GetFromJsonAsync<ProductDetailDto>($"/catalog/products/{product.Id}");

        detail!.Name.Should().Be("Cà phê sữa đá");
        detail.Description.Should().Be("A drink.");
        detail.Price.Current.AmountMinor.Should().Be(50_000L);
        detail.Price.Current.CurrencyCode.Should().Be("VND");
        detail.StockQuantity.Should().Be(7);
        detail.IsOutOfStock.Should().BeFalse();
        detail.Images.Should().HaveCount(2);
        detail.Images[0].Url.Should().Be("https://img/a.png", "the primary image leads the gallery");
        detail.Categories.Select(c => c.Name).Should().BeEquivalentTo(["Coffee", "Gifts"]);
    }

    [Fact]
    public async Task Renders_a_product_that_has_no_images()
    {
        await fixture.ResetAsync();
        var product = CatalogFixture.NewProduct("Imageless");
        await fixture.WithDbAsync(async db => { db.Add(product); await db.SaveChangesAsync(); });

        var detail = await fixture.CreateClient()
            .GetFromJsonAsync<ProductDetailDto>($"/catalog/products/{product.Id}");

        detail!.Images.Should().BeEmpty();
        detail.PrimaryImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task A_zero_stock_product_is_openable_and_flagged()
    {
        await fixture.ResetAsync();
        var product = CatalogFixture.NewProduct("Sold out", stock: 0);
        await fixture.WithDbAsync(async db => { db.Add(product); await db.SaveChangesAsync(); });

        var detail = await fixture.CreateClient()
            .GetFromJsonAsync<ProductDetailDto>($"/catalog/products/{product.Id}");

        detail!.IsOutOfStock.Should().BeTrue();
        detail.StockQuantity.Should().Be(0);
    }
}
