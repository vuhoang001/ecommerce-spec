using System.Net;
using ECommerce.Catalog.Application.Contracts;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>US3/AC4 — FR-019: an empty keyword is a stated rejection, not the whole catalogue.</summary>
[Collection("catalog")]
public class SearchValidationTests(CatalogFixture fixture)
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_or_whitespace_keyword_is_rejected_with_a_reason(string keyword)
    {
        await fixture.ResetAsync();
        await fixture.WithDbAsync(async db =>
        {
            db.Add(CatalogFixture.NewProduct("Should not be returned"));
            await db.SaveChangesAsync();
        });

        var response = await fixture.CreateClient()
            .GetAsync($"/catalog/products/search?q={Uri.EscapeDataString(keyword)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(ReasonCodes.EmptyKeyword);
        body.Should().NotContain("Should not be returned", "an empty keyword never returns the catalogue");
    }

    [Fact]
    public async Task A_missing_keyword_parameter_is_rejected_the_same_way()
    {
        await fixture.ResetAsync();
        var response = await fixture.CreateClient().GetAsync("/catalog/products/search");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(ReasonCodes.EmptyKeyword);
    }
}
