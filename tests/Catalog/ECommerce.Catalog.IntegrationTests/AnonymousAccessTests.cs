using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// FR-034, SC-013 — every catalogue read path is reachable without credentials, and none reads
/// a customer identity. If someone later puts an endpoint behind auth, this is what fails.
/// </summary>
[Collection("catalog")]
public class AnonymousAccessTests(CatalogFixture fixture)
{
    public static TheoryData<string> EveryReadPath() =>
    [
        $"/catalog/categories/{Guid.NewGuid()}/products",
        $"/catalog/products/{Guid.NewGuid()}",
        "/catalog/products/search?q=coffee",
        "/catalog/products?minPriceMinor=0"
    ];

    [Theory]
    [MemberData(nameof(EveryReadPath))]
    public async Task Every_read_path_answers_without_credentials(string path)
    {
        await fixture.ResetAsync();
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.GetAsync(path);

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "FR-034: browsing never requires a sign-in");
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(EveryReadPath))]
    public async Task Supplying_a_bogus_credential_changes_nothing(string path)
    {
        // No read path depends on a customer identity, so an unrecognised token must be ignored
        // rather than rejected — otherwise the endpoint is reading an identity it should not.
        await fixture.ResetAsync();
        var client = fixture.CreateClient();

        var anonymous = await client.GetAsync(path);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-real-token");
        var withToken = await client.GetAsync(path);

        withToken.StatusCode.Should().Be(anonymous.StatusCode,
            "SC-013: the response does not depend on who is asking");
    }
}
