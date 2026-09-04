using ECommerce.Catalog.Application.Detail;
using ECommerce.Host.Errors;
using ECommerce.Host.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Host.Endpoints;

/// <summary>US2 — FR-009, FR-002. Anonymous, like every catalogue read path (FR-034).</summary>
public static class ProductDetailEndpoint
{
    public static IEndpointRouteBuilder MapProductDetail(this IEndpointRouteBuilder app)
    {
        app.MapGet("/catalog/products/{productId:guid}", async (
                Guid productId,
                GetProductDetailQuery query,
                CancellationToken ct) =>
            {
                var result = await query.ExecuteAsync(productId, ct);

                // FR-002: a Hidden or Discontinued product is reported exactly as one that
                // never existed — same status, same reason code, nothing disclosed.
                return result.Succeeded
                    ? Results.Ok(result.Value)
                    : CatalogProblemDetails.From(result.ReasonCode!, result.Detail);
            })
            .WithName("GetProductDetail")
            .RequireRateLimiting(CatalogRateLimiter.PolicyName);

        return app;
    }
}
