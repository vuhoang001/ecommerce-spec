using Dapper;
using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Application.Pricing;
using ECommerce.Catalog.Application.Reads;
using ECommerce.Shared.Kernel.Primitives;

namespace ECommerce.Catalog.Application.Filter;

/// <summary>
/// FR-021..FR-028 — category and price range, combinable, matching on the original price, the
/// discounted price, or both, returning each product exactly once.
/// </summary>
/// <remarks>
/// research.md R1: the discounted price is a column Catalog owns — the discount copy — because
/// DAT-001 forbids reading Promotion's tables.
/// DAT-004 Dapper; DAT-005 shared visibility fragment; DAT-006 catalog schema only.
/// </remarks>
public sealed class FilterProductsQuery(
    ICatalogReadConnection connections, IClock clock, CatalogPricingOptions options)
{
    public async Task<Result<ProductPageDto>> ExecuteAsync(
        Guid? categoryId, long? minMinor, long? maxMinor, int? page, int? pageSize,
        CancellationToken ct = default)
    {
        var (valid, reasonCode, detail) =
            PriceRangeValidator.Validate(new PriceRangeValidator.Range(minMinor, maxMinor));
        if (!valid)
            return Result<ProductPageDto>.Fail(reasonCode!, detail);

        var (p, size) = Paging.Normalise(page, pageSize);
        using var connection = await connections.OpenAsync(ct);

        // FR-015: a copy past the staleness limit is not used by the filter either.
        var freshFrom = clock.UtcNow - options.DiscountStalenessLimit;

        var clauses = new List<string> { CatalogVisibility.ActiveOnly("p") };
        if (categoryId is not null)
            clauses.Add("EXISTS (SELECT 1 FROM catalog.product_category pc " +
                        "WHERE pc.product_id = p.id AND pc.category_id = @categoryId)");

        // FR-023 inclusive; FR-026 either price; FR-027 list price alone when no copy is known.
        if (minMinor is not null && maxMinor is not null)
            clauses.Add("((p.price_minor BETWEEN @minMinor AND @maxMinor) OR " +
                        "(d.discounted_price_minor BETWEEN @minMinor AND @maxMinor))");
        else if (minMinor is not null)
            clauses.Add("(p.price_minor >= @minMinor OR d.discounted_price_minor >= @minMinor)");
        else if (maxMinor is not null)
            clauses.Add("(p.price_minor <= @maxMinor OR d.discounted_price_minor <= @maxMinor)");

        var where = $"""
            FROM catalog.product p
            LEFT JOIN catalog.discount_projection d
                   ON d.product_id = p.id AND d.retrieved_at >= @freshFrom
            WHERE {string.Join(" AND ", clauses)}
            """;

        var args = new
        {
            categoryId, minMinor, maxMinor, freshFrom,
            offset = (p - 1) * size, limit = size
        };

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT count(*) {where}", args, cancellationToken: ct));

        var rows = await connection.QueryAsync<ProductReadRow>(new CommandDefinition($"""
            SELECT p.id                        AS "Id",
                   p.name                      AS "Name",
                   p.price_minor               AS "PriceMinor",
                   p.currency_code             AS "CurrencyCode",
                   p.stock_quantity            AS "StockQuantity",
                   d.discounted_price_minor    AS "DiscountedPriceMinor",
                   (SELECT i.url FROM catalog.product_image i
                     WHERE i.product_id = p.id
                     ORDER BY i.is_primary DESC, i.position
                     LIMIT 1)                  AS "PrimaryImageUrl"
            {where}
            ORDER BY p.created_at DESC, p.id
            OFFSET @offset LIMIT @limit
            """, args, cancellationToken: ct));

        var items = rows.Select(r =>
        {
            var listInRange = InRange(r.PriceMinor, minMinor, maxMinor);
            var discountedInRange = r.DiscountedPriceMinor is { } c && InRange(c, minMinor, maxMinor);

            // FR-028: a product the discounted price alone brought in shows both prices.
            var matchedOnDiscountOnly = discountedInRange && !listInRange;

            var price = r.DiscountedPriceMinor is { } discounted
                ? new PriceDisplayDto(
                    new MoneyDto(discounted, r.CurrencyCode),
                    new MoneyDto(r.PriceMinor, r.CurrencyCode), IsOutOfDate: false)
                : new PriceDisplayDto(new MoneyDto(r.PriceMinor, r.CurrencyCode), null, false);

            return new ProductSummaryDto(
                r.Id, r.Name, r.PrimaryImageUrl, price, r.StockQuantity == 0, matchedOnDiscountOnly);
        }).ToList();

        return Result<ProductPageDto>.Ok(new ProductPageDto(items, p, size, total,
            items.Count == 0
                ? total == 0 ? ReasonCodes.NoMatches : ReasonCodes.PageBeyondLast
                : null));
    }

    private static bool InRange(long amount, long? min, long? max)
        => (min is not { } lo || amount >= lo) && (max is not { } hi || amount <= hi);
}
