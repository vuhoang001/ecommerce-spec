using Dapper;
using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Application.Reads;
using ECommerce.Shared.Kernel.Primitives;

namespace ECommerce.Catalog.Application.Search;

/// <summary>
/// FR-017..FR-020 — partial name match, ignoring letter case and diacritics in both directions.
/// </summary>
/// <remarks>
/// research.md R3: the stored name is normalised into a generated column and the keyword through
/// the same SQL function, so the match works in both directions from one GIN trigram index.
/// DAT-004 Dapper, DAT-005 shared visibility fragment, DAT-006 catalog schema only.
/// </remarks>
public sealed class SearchProductsQuery(ICatalogReadConnection connections)
{
    public async Task<Result<ProductPageDto>> ExecuteAsync(
        string? keyword, int? page, int? pageSize, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Result<ProductPageDto>.Fail(
                ReasonCodes.EmptyKeyword,
                "A search keyword is required; an empty keyword does not return the catalogue.");

        var (p, size) = Paging.Normalise(page, pageSize);
        using var connection = await connections.OpenAsync(ct);

        var where = $"""
            FROM catalog.product p
            WHERE p.name_normalized LIKE '%' || catalog.normalise_name(@keyword) || '%'
              AND {CatalogVisibility.ActiveOnly("p")}
            """;

        var args = new { keyword = keyword.Trim(), offset = (p - 1) * size, limit = size };

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*) {where}", args, cancellationToken: ct));

        var rows = await connection.QueryAsync<ProductReadRow>(new CommandDefinition($"""
            SELECT p.id             AS "Id",
                   p.name           AS "Name",
                   p.price_minor    AS "PriceMinor",
                   p.currency_code  AS "CurrencyCode",
                   p.stock_quantity AS "StockQuantity",
                   (SELECT i.url FROM catalog.product_image i
                     WHERE i.product_id = p.id
                     ORDER BY i.is_primary DESC, i.position
                     LIMIT 1)       AS "PrimaryImageUrl"
            {where}
            ORDER BY p.name, p.id
            OFFSET @offset LIMIT @limit
            """, args, cancellationToken: ct));

        var items = rows.Select(r => new ProductSummaryDto(
            r.Id, r.Name, r.PrimaryImageUrl,
            new PriceDisplayDto(new MoneyDto(r.PriceMinor, r.CurrencyCode), null, false),
            r.StockQuantity == 0)).ToList();

        return Result<ProductPageDto>.Ok(new ProductPageDto(items, p, size, total,
            items.Count == 0
                ? total == 0 ? ReasonCodes.NoMatches : ReasonCodes.PageBeyondLast
                : null));
    }
}
