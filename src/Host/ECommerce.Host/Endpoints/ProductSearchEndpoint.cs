using ECommerce.Catalog.Application.Search;
using ECommerce.Host.Errors;
using ECommerce.Host.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Host.Endpoints;

/// <summary>US3 — FR-017, FR-019, FR-020.</summary>
public static class ProductSearchEndpoint
{
    public static IEndpointRouteBuilder MapProductSearch(this IEndpointRouteBuilder app)
    {
        app.MapGet("/catalog/products/search", async (
                string? q,
                int? page,
                int? pageSize,
                SearchProductsQuery query,
                CancellationToken ct) =>
            {
                var result = await query.ExecuteAsync(q, page, pageSize, ct);

                // FR-019: an empty keyword is a stated rejection, never the whole catalogue.
                return result.Succeeded
                    ? Results.Ok(result.Value)
                    : CatalogProblemDetails.From(result.ReasonCode!, result.Detail);
            })
            .WithName("SearchProducts")
            .RequireRateLimiting(CatalogRateLimiter.PolicyName);

        return app;
    }
}
