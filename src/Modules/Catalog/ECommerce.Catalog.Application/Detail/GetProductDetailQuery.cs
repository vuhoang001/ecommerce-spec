using Dapper;
using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Application.Pricing;
using ECommerce.Catalog.Application.Reads;
using ECommerce.Shared.Kernel.Primitives;

namespace ECommerce.Catalog.Application.Detail;

/// <summary>
/// FR-009, FR-002 — everything a customer needs to decide, with the price resolved through
/// live Promotion, then the discount copy, then the list price.
/// </summary>
/// <remarks>
/// DAT-004 Dapper; DAT-005 shared visibility fragment, which is what makes a Hidden or
/// Discontinued product report identically to one that never existed; DAT-006 catalog schema.
/// </remarks>
public sealed class GetProductDetailQuery(ICatalogReadConnection connections, ProductPriceResolver prices)
{
    public async Task<Result<ProductDetailDto>> ExecuteAsync(Guid productId, CancellationToken ct = default)
    {
        using var connection = await connections.OpenAsync(ct);

        var product = await connection.QuerySingleOrDefaultAsync<ProductReadRow>(
            new CommandDefinition($"""
                SELECT p.id             AS "Id",
                       p.name           AS "Name",
                       p.description    AS "Description",
                       p.price_minor    AS "PriceMinor",
                       p.currency_code  AS "CurrencyCode",
                       p.stock_quantity AS "StockQuantity",
                       (SELECT i.url FROM catalog.product_image i
                         WHERE i.product_id = p.id
                         ORDER BY i.is_primary DESC, i.position
                         LIMIT 1)       AS "PrimaryImageUrl"
                FROM catalog.product p
                WHERE p.id = @productId AND {CatalogVisibility.ActiveOnly("p")}
                """, new { productId }, cancellationToken: ct));

        // FR-002: a non-Active product simply is not here, so it is reported exactly as one that
        // never existed — same status, same reason code, nothing disclosed.
        if (product is null)
            return Result<ProductDetailDto>.Fail(ReasonCodes.ProductNotFound, "No such product.");

        var categories = (await connection.QueryAsync<CategoryRefDto>(new CommandDefinition("""
            SELECT c.id AS "Id", c.name AS "Name", c.slug AS "Slug"
            FROM catalog.category c
            JOIN catalog.product_category pc ON pc.category_id = c.id
            WHERE pc.product_id = @productId
            ORDER BY c.name
            """, new { productId }, cancellationToken: ct))).ToList();

        var images = (await connection.QueryAsync<ProductImageDto>(new CommandDefinition("""
            SELECT i.url AS "Url", i.position AS "Position"
            FROM catalog.product_image i
            WHERE i.product_id = @productId
            ORDER BY i.is_primary DESC, i.position
            """, new { productId }, cancellationToken: ct))).ToList();

        var price = await prices.ResolveAsync(product.Id, product.PriceMinor, product.CurrencyCode, ct);

        return Result<ProductDetailDto>.Ok(new ProductDetailDto(
            product.Id, product.Name, product.PrimaryImageUrl, price,
            product.StockQuantity == 0, product.Description, product.StockQuantity,
            categories, images));
    }
}
