using ECommerce.Catalog.Application.Filter;
using ECommerce.Host.Errors;
using ECommerce.Host.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Host.Endpoints;

/// <summary>US4 — FR-021, FR-028.</summary>
public static class ProductFilterEndpoint
{
    public static IEndpointRouteBuilder MapProductFilter(this IEndpointRouteBuilder app)
    {
        app.MapGet("/catalog/products", async (
                Guid? categoryId,
                long? minPriceMinor,
                long? maxPriceMinor,
                int? page,
                int? pageSize,
                FilterProductsQuery query,
                CancellationToken ct) =>
            {
                var result = await query.ExecuteAsync(
                    categoryId, minPriceMinor, maxPriceMinor, page, pageSize, ct);

                // FR-022 / FR-029: an invalid range is a stated error, never an empty list.
                return result.Succeeded
                    ? Results.Ok(result.Value)
                    : CatalogProblemDetails.From(result.ReasonCode!, result.Detail);
            })
            .WithName("FilterProducts")
            .RequireRateLimiting(CatalogRateLimiter.PolicyName);

        return app;
    }
}
