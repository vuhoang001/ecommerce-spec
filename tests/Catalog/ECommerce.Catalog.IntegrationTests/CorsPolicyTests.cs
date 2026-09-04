using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// UIX-001 / UIX-002 — the frontend is a separate deployable consuming this backend over HTTP,
/// so the browser treats every call as cross-origin. Without a policy it blocks them before any
/// application code runs.
/// </summary>
/// <remarks>
/// The default is to allow NOTHING. An unset configuration must fail closed: allowing every
/// origin by default would be a security posture nobody chose.
/// </remarks>
[Collection("catalog")]
public class CorsPolicyTests(CatalogFixture fixture)
{
    private const string Configured = "https://storefront.example";
    private const string Unconfigured = "https://not-the-storefront.example";
    private const string SomePath = "/catalog/products/search?q=coffee";

    private HttpClient ClientAllowing(params string[] origins)
    {
        // UseSetting writes into the host's configuration before Program reads it, which
        // ConfigureAppConfiguration does not reliably do for a top-level-statements Program.
        var factory = fixture.WithWebHostBuilder(builder =>
        {
            for (var i = 0; i < origins.Length; i++)
                builder.UseSetting($"Cors:AllowedOrigins:{i}", origins[i]);
        });

        return factory.CreateClient();
    }

    [Fact]
    public async Task A_configured_origin_is_allowed()
    {
        await fixture.ResetAsync();
        var client = ClientAllowing(Configured);

        var request = new HttpRequestMessage(HttpMethod.Get, SomePath);
        request.Headers.Add("Origin", Configured);
        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed)
            .Should().BeTrue("a configured origin must be told it may read the response");
        allowed!.Should().Contain(Configured);
    }

    [Fact]
    public async Task An_unconfigured_origin_is_not_allowed()
    {
        await fixture.ResetAsync();
        var client = ClientAllowing(Configured);

        var request = new HttpRequestMessage(HttpMethod.Get, SomePath);
        request.Headers.Add("Origin", Unconfigured);
        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin")
            .Should().BeFalse("only origins the operator named may read the response");
    }

    [Fact]
    public async Task With_no_origin_configured_nothing_is_allowed()
    {
        // Fail closed. An unset value must not mean "allow the world".
        await fixture.ResetAsync();
        var client = ClientAllowing();

        var request = new HttpRequestMessage(HttpMethod.Get, SomePath);
        request.Headers.Add("Origin", Configured);
        var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task The_retry_after_header_is_exposed_to_the_browser()
    {
        // FR-035: a refused caller must read Retry-After to back off. Cross-origin, a header the
        // server sends is invisible to script unless it is explicitly exposed.
        await fixture.ResetAsync();
        var client = ClientAllowing(Configured);

        var request = new HttpRequestMessage(HttpMethod.Get, SomePath);
        request.Headers.Add("Origin", Configured);
        var response = await client.SendAsync(request);

        response.Headers.TryGetValues("Access-Control-Expose-Headers", out var exposed)
            .Should().BeTrue();
        string.Join(",", exposed!).Should().Contain("Retry-After");
    }
}
