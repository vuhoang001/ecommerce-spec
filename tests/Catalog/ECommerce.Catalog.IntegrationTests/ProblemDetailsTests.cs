using System.Net;
using System.Text.Json;
using ECommerce.Catalog.Application.Contracts;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// FR-029 / SC-005 — every rejection carries a reason code the caller can act on, and a
/// rejection is never answered with a silently empty result.
/// </summary>
[Collection("catalog")]
public class ProblemDetailsTests(CatalogFixture fixture)
{
    [Fact]
    public async Task A_rejection_carries_a_machine_readable_reason_code()
    {
        await fixture.ResetAsync();
        var client = fixture.CreateClient();

        var response = await client.GetAsync($"/catalog/categories/{Guid.NewGuid()}/products");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("reasonCode").GetString()
            .Should().Be(ReasonCodes.CategoryNotFound);
        document.RootElement.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_rejection_is_never_answered_with_an_empty_list()
    {
        await fixture.ResetAsync();
        var client = fixture.CreateClient();

        var response = await client.GetAsync($"/catalog/categories/{Guid.NewGuid()}/products");
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeFalse(
            "returning an empty result in place of an error is FORBIDDEN (FR-029)");
        body.Should().NotContain("\"items\"");
    }
}
