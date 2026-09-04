using System.Net.Http.Json;
using ECommerce.Catalog.Application.Contracts;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// US3/AC1 — FR-017: partial match on the product name, ignoring letter case and ignoring
/// diacritics in BOTH directions.
/// </summary>
[Collection("catalog")]
public class SearchDiacriticTests(CatalogFixture fixture)
{
    private async Task SeedAsync()
    {
        await fixture.ResetAsync();
        await fixture.WithDbAsync(async db =>
        {
            db.Add(CatalogFixture.NewProduct("Cà phê sữa đá"));
            db.Add(CatalogFixture.NewProduct("Trà đào cam sả"));
            db.Add(CatalogFixture.NewProduct("Plain Water"));
            await db.SaveChangesAsync();
        });
    }

    [Theory]
    [InlineData("ca phe")]     // keyword without diacritics matches a name with them
    [InlineData("CÀ PHÊ")]     // upper case with diacritics
    [InlineData("cà phê")]     // exact
    [InlineData("PHE")]        // infix, upper case, no diacritics
    [InlineData("sữa")]        // infix with diacritics
    public async Task Finds_the_vietnamese_product_however_the_keyword_is_written(string keyword)
    {
        await SeedAsync();
        var page = await fixture.CreateClient()
            .GetFromJsonAsync<ProductPageDto>($"/catalog/products/search?q={Uri.EscapeDataString(keyword)}");

        page!.Items.Should().ContainSingle(i => i.Name == "Cà phê sữa đá",
            "'{0}' must match through the same normalisation the stored name went through", keyword);
    }

    [Fact]
    public async Task Does_not_match_an_unrelated_product()
    {
        await SeedAsync();
        var page = await fixture.CreateClient()
            .GetFromJsonAsync<ProductPageDto>("/catalog/products/search?q=water");

        page!.Items.Should().ContainSingle().Which.Name.Should().Be("Plain Water");
    }

    [Fact]
    public async Task States_the_reason_when_nothing_matches()
    {
        await SeedAsync();
        var page = await fixture.CreateClient()
            .GetFromJsonAsync<ProductPageDto>("/catalog/products/search?q=zzzznothing");

        page!.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        page.EmptyReason.Should().Be(ReasonCodes.NoMatches);
    }
}
