using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.RepresentationModel;

namespace ECommerce.Catalog.IntegrationTests;

/// <summary>
/// UIX-002 — the frontend consumes the backend only through the published OpenAPI contract.
/// </summary>
/// <remarks>
/// The contract is checked in by hand, and the host does not emit one, so nothing previously
/// stopped the document and the implementation drifting apart — a generated client would then be
/// wrong in a way no test could see. This compares the documented paths against the routes the
/// host actually registers, in both directions: a documented route that does not exist, and a
/// route nobody documented, both fail.
/// </remarks>
[Collection("catalog")]
public class OpenApiContractConformanceTests(CatalogFixture fixture)
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ECommerce.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    /// <summary>Paths declared in the checked-in contract, normalised to comparable shapes.</summary>
    private static HashSet<string> DocumentedPaths()
    {
        var contract = Path.Combine(RepoRoot(), "specs", "002-product-catalog", "contracts",
            "catalog-storefront.openapi.yaml");

        using var reader = new StreamReader(contract);
        var yaml = new YamlStream();
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var paths = (YamlMappingNode)root.Children[new YamlScalarNode("paths")];

        return paths.Children.Keys
            .OfType<YamlScalarNode>()
            .Select(k => Normalise(k.Value!))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Routes the host actually serves.</summary>
    private HashSet<string> RegisteredRoutes()
    {
        using var scope = fixture.Services.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();

        return sources.Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/'))
            // The document endpoint describes the API; it is not part of the API surface, so
            // documenting it inside itself would be circular.
            .Where(path => !path.Contains("openapi.json", StringComparison.Ordinal))
            .Select(Normalise)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// OpenAPI writes <c>{productId}</c>; ASP.NET Core writes <c>{productId:guid}</c>. Reduce
    /// both to a placeholder so the comparison is about the route, not its constraint syntax.
    /// </summary>
    private static string Normalise(string path) =>
        Regex.Replace(path, @"\{[^}]+\}", "{}").TrimEnd('/');

    [Fact]
    public void Every_documented_path_is_a_route_the_host_actually_serves()
    {
        var undelivered = DocumentedPaths().Except(RegisteredRoutes()).OrderBy(p => p).ToList();

        undelivered.Should().BeEmpty(
            "a client generated from this contract would call a route that does not exist");
    }

    [Fact]
    public void Every_route_the_host_serves_is_documented()
    {
        var undocumented = RegisteredRoutes().Except(DocumentedPaths()).OrderBy(p => p).ToList();

        undocumented.Should().BeEmpty(
            "UIX-002 makes the contract the only sanctioned way in; an undocumented route is a " +
            "way in nobody agreed to");
    }

    [Fact]
    public void The_contract_declares_the_rate_limit_response_every_endpoint_can_return()
    {
        // FR-035: every read path is rate limited, so 429 is reachable on all of them. A client
        // generated without it would treat a refusal as an unexpected failure.
        var contract = File.ReadAllText(Path.Combine(RepoRoot(), "specs", "002-product-catalog",
            "contracts", "catalog-storefront.openapi.yaml"));

        var storefrontPaths = DocumentedPaths().Count(p => p.StartsWith("/catalog", StringComparison.Ordinal));
        Regex.Matches(contract, @"'429'").Count
            .Should().Be(storefrontPaths, "every storefront endpoint can refuse an over-limit caller");
    }

    [Fact]
    public async Task The_published_document_declares_the_same_paths_as_the_checked_in_contract()
    {
        // UIX-002: the frontend generates its client from a published document. If that document
        // and the checked-in contract disagree, the generated client is wrong and nothing else
        // would notice — the hand-written file cannot drift from the server on its own.
        var json = await fixture.CreateClient().GetStringAsync("/openapi.json");
        using var published = JsonDocument.Parse(json);

        var publishedPaths = published.RootElement.GetProperty("paths")
            .EnumerateObject()
            .Select(p => Normalise(p.Name))
            .ToHashSet(StringComparer.Ordinal);

        publishedPaths.Should().BeEquivalentTo(DocumentedPaths(),
            "the published document and the checked-in contract must describe one API");
    }

    [Fact]
    public async Task The_published_document_is_valid_openapi_with_a_version_and_paths()
    {
        var json = await fixture.CreateClient().GetStringAsync("/openapi.json");
        using var document = JsonDocument.Parse(json);

        document.RootElement.GetProperty("openapi").GetString().Should().StartWith("3.");
        document.RootElement.GetProperty("paths").EnumerateObject().Should().NotBeEmpty();
    }
}
