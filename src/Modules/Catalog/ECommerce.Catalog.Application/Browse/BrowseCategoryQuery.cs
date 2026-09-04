using Dapper;
using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Application.Reads;
using ECommerce.Shared.Kernel.Primitives;

namespace ECommerce.Catalog.Application.Browse;

/// <summary>
/// FR-003..FR-008 — a page of the Active products in one category, always stating the total and
/// the page position.
/// </summary>
/// <remarks>
/// DAT-004: this is a read, so it executes through Dapper and never calls SaveChanges.
/// DAT-005: visibility comes from <see cref="CatalogVisibility.ActiveOnly"/>, never a
/// hand-written clause.
/// DAT-006: every table named here lives in the catalog schema.
/// </remarks>
public sealed class BrowseCategoryQuery(ICatalogReadConnection connections)
{
    public async Task<Result<ProductPageDto>> ExecuteAsync(
        Guid categoryId, int? page, int? pageSize, CancellationToken ct = default)
    {
        var (p, size) = Paging.Normalise(page, pageSize);
        using var connection = await connections.OpenAsync(ct);

        var categoryExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM catalog.category c WHERE c.id = @categoryId)",
            new { categoryId }, cancellationToken: ct));

        if (!categoryExists)
            return Result<ProductPageDto>.Fail(ReasonCodes.CategoryNotFound, "No such category.");

        var where = $"""
            FROM catalog.product p
            JOIN catalog.product_category pc ON pc.product_id = p.id
            WHERE pc.category_id = @categoryId AND {CatalogVisibility.ActiveOnly("p")}
            """;

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*) {where}", new { categoryId }, cancellationToken: ct));

        var rows = await connection.QueryAsync<ProductReadRow>(new CommandDefinition($"""
            SELECT p.id                AS "Id",
                   p.name              AS "Name",
                   p.price_minor       AS "PriceMinor",
                   p.currency_code     AS "CurrencyCode",
                   p.stock_quantity    AS "StockQuantity",
                   (SELECT i.url FROM catalog.product_image i
                     WHERE i.product_id = p.id
                     ORDER BY i.is_primary DESC, i.position
                     LIMIT 1)          AS "PrimaryImageUrl"
            {where}
            ORDER BY p.created_at DESC, p.id
            OFFSET @offset LIMIT @limit
            """, new { categoryId, offset = (p - 1) * size, limit = size }, cancellationToken: ct));

        var items = rows.Select(r => new ProductSummaryDto(
            r.Id, r.Name, r.PrimaryImageUrl,
            new PriceDisplayDto(new MoneyDto(r.PriceMinor, r.CurrencyCode), null, false),
            r.StockQuantity == 0)).ToList();

        return Result<ProductPageDto>.Ok(new ProductPageDto(items, p, size, total,
            items.Count == 0
                ? total == 0 ? ReasonCodes.NoProductsInCategory : ReasonCodes.PageBeyondLast
                : null));
    }
}
