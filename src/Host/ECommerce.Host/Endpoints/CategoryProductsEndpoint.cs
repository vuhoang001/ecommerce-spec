using ECommerce.Catalog.Application.Browse;
using ECommerce.Host.Errors;
using ECommerce.Host.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Host.Endpoints;

/// <summary>
/// US1 — browse the Active products of a category (FR-003, FR-004, FR-005, FR-006, FR-007).
/// Anonymous: no read path reads a customer identity (FR-034).
/// </summary>
public static class CategoryProductsEndpoint
{
    public static IEndpointRouteBuilder MapCategoryProducts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/catalog/categories/{categoryId:guid}/products", async (
                Guid categoryId,
                int? page,
                int? pageSize,
                BrowseCategoryQuery query,
                CancellationToken ct) =>
            {
                var result = await query.ExecuteAsync(categoryId, page, pageSize, ct);

                // FR-008 / FR-029: an empty page carries its reason inside a 200; a genuine
                // rejection carries a reason code. Neither is ever a silent empty list.
                return result.Succeeded
                    ? Results.Ok(result.Value)
                    : CatalogProblemDetails.From(result.ReasonCode!, result.Detail);
            })
            .WithName("BrowseCategoryProducts")
            .RequireRateLimiting(CatalogRateLimiter.PolicyName);

        return app;
    }
}
