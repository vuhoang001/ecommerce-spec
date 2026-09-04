using System.Net;
using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Domain;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// US2/AC4 — FR-002: a Hidden or Discontinued product is reported exactly as one that never
/// existed. Same status, same reason code, same body — nothing discloses that it exists.
/// </summary>
[Collection("catalog")]
public class ProductDetailVisibilityTests(CatalogFixture fixture)
{
    [Theory]
    [InlineData(ProductStatus.Hidden)]
    [InlineData(ProductStatus.Discontinued)]
    [InlineData(ProductStatus.Draft)]
    public async Task A_non_active_product_is_indistinguishable_from_one_that_never_existed(
        ProductStatus status)
    {
        await fixture.ResetAsync();
        var product = CatalogFixture.NewProduct("Concealed", status: status);
        await fixture.WithDbAsync(async db => { db.Add(product); await db.SaveChangesAsync(); });

        var client = fixture.CreateClient();
        var concealed = await client.GetAsync($"/catalog/products/{product.Id}");
        var neverExisted = await client.GetAsync($"/catalog/products/{Guid.NewGuid()}");

        concealed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        concealed.StatusCode.Should().Be(neverExisted.StatusCode);

        var concealedBody = await concealed.Content.ReadAsStringAsync();
        var neverExistedBody = await neverExisted.Content.ReadAsStringAsync();

        concealedBody.Should().Contain(ReasonCodes.ProductNotFound);
        concealedBody.Should().Be(neverExistedBody,
            "the two responses must be byte-identical or the difference discloses existence");
        concealedBody.Should().NotContain("Concealed", "the name must not leak");
    }
}
