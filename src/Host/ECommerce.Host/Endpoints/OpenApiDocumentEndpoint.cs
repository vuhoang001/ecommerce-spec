using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Host.Endpoints;

/// <summary>
/// UIX-002 — publishes the OpenAPI document the frontend generates its client from.
/// </summary>
/// <remarks>
/// The document is built from the host's own routing metadata, so it cannot describe a route the
/// application does not serve. That is the whole point: the checked-in contract was hand-written,
/// and nothing detected it drifting from the implementation — a generated client would then be
/// wrong in a way no test could see.
/// <para>
/// Deliberately written against first-party types only. Swashbuckle or NSwag would produce a
/// richer document and would also be a new runtime component, which STK-001 closes off without an
/// amendment. Paths and methods are what a client needs to be correct about, and they are exactly
/// what routing metadata knows for certain.
/// </para>
/// </remarks>
public static class OpenApiDocumentEndpoint
{
    public static IEndpointRouteBuilder MapOpenApiDocument(this IEndpointRouteBuilder app)
    {
        app.MapGet("/openapi.json", (EndpointDataSource endpoints) =>
        {
            var paths = new JsonObject();

            foreach (var endpoint in endpoints.Endpoints.OfType<RouteEndpoint>())
            {
                var pattern = endpoint.RoutePattern.RawText;
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                if (pattern.Contains("openapi.json", StringComparison.Ordinal)) continue;

                // OpenAPI writes {id}; ASP.NET Core writes {id:guid}. Strip the constraint so the
                // published document matches the contract's vocabulary.
                var path = "/" + string.Join('/', pattern.TrimStart('/').Split('/')
                    .Select(segment => segment.StartsWith('{')
                        ? "{" + segment.Trim('{', '}').Split(':')[0] + "}"
                        : segment));

                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                              ?? ["GET"];

                var operations = paths[path] as JsonObject;
                if (operations is null)
                {
                    operations = new JsonObject();
                    paths[path] = operations;
                }

                foreach (var method in methods)
                {
                    operations[method.ToLowerInvariant()] = new JsonObject
                    {
                        ["operationId"] = endpoint.DisplayName,
                        ["responses"] = new JsonObject
                        {
                            ["200"] = new JsonObject { ["description"] = "Success" }
                        }
                    };
                }
            }

            var document = new JsonObject
            {
                ["openapi"] = "3.1.0",
                ["info"] = new JsonObject
                {
                    ["title"] = "Catalog Storefront Read API",
                    ["version"] = "1.0.0",
                    ["description"] = "Generated from the host's routing metadata (UIX-002)."
                },
                ["paths"] = paths
            };

            return Results.Text(
                document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                "application/json");
        })
        .WithName("OpenApiDocument")
        .ExcludeFromDescription();

        return app;
    }
}
