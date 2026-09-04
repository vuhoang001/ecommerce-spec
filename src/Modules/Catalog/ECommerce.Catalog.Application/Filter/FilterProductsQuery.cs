using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Application.Pricing;
using ECommerce.Catalog.Domain;
using ECommerce.Shared.Kernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.Application.Filter;

/// <summary>
/// US4 — FR-021, FR-023, FR-026, FR-027, FR-028: category and price range, combinable, matching
/// on the original price, the discounted price, or both.
/// </summary>
/// <remarks>
/// research.md R1: the discounted price belongs to Promotion, and DAT-001 forbids Catalog from
/// reading Promotion's tables. The discount copy is what makes this query possible at all — it
/// is a column Catalog owns and can therefore filter on, in one left join, with each product
/// returned exactly once.
/// </remarks>
public sealed class FilterProductsQuery(DbContext db, IClock clock, CatalogPricingOptions options)
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

        // FR-015: a copy past the staleness limit is not used by the filter either.
        var freshFrom = clock.UtcNow - options.DiscountStalenessLimit;

        var rows = db.Set<Product>()
            .Select(x => new
            {
                Product = x,
                PriceMinor = EF.Property<long>(x, "_priceMinor"),
                Currency = EF.Property<string>(x, "_currencyCode"),
                Copy = db.Set<DiscountProjection>()
                    .Where(d => d.ProductId == x.Id && d.RetrievedAt >= freshFrom)
                    .Select(d => (long?)EF.Property<long>(d, "_discountedPriceMinor"))
                    .FirstOrDefault()
            });

        if (categoryId is { } cid)
            rows = rows.Where(r => r.Product.Categories.Any(c => c.Id == cid));

        // FR-023 bounds are inclusive; FR-026 matches on either price; FR-027 falls back to the
        // original price alone when no discounted price is known.
        if (minMinor is { } min)
            rows = rows.Where(r => r.PriceMinor >= min || (r.Copy != null && r.Copy >= min));
        if (maxMinor is { } max)
            rows = rows.Where(r => r.PriceMinor <= max || (r.Copy != null && r.Copy <= max));

        if (minMinor is { } lo && maxMinor is { } hi)
        {
            rows = rows.Where(r =>
                (r.PriceMinor >= lo && r.PriceMinor <= hi) ||
                (r.Copy != null && r.Copy >= lo && r.Copy <= hi));
        }

        var total = await rows.CountAsync(ct);

        var items = await rows
            .OrderByDescending(r => r.Product.CreatedAt)
            .ThenBy(r => r.Product.Id)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(r => new
            {
                r.Product.Id,
                r.Product.Name,
                r.Product.StockQuantity,
                r.PriceMinor,
                r.Currency,
                r.Copy,
                PrimaryImageUrl = r.Product.Images
                    .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Position)
                    .Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync(ct);

        var summaries = items.Select(x =>
        {
            var listInRange = InRange(x.PriceMinor, minMinor, maxMinor);
            var discountedInRange = x.Copy is { } c && InRange(c, minMinor, maxMinor);

            // FR-028: a product the discounted price alone brought in shows both prices, so the
            // customer can see why it appeared.
            var matchedOnDiscountOnly = discountedInRange && !listInRange;

            var price = x.Copy is { } discounted
                ? new PriceDisplayDto(
                    new MoneyDto(discounted, x.Currency),
                    new MoneyDto(x.PriceMinor, x.Currency),
                    IsOutOfDate: false)
                : new PriceDisplayDto(new MoneyDto(x.PriceMinor, x.Currency), null, false);

            return new ProductSummaryDto(
                x.Id, x.Name, x.PrimaryImageUrl, price, x.StockQuantity == 0, matchedOnDiscountOnly);
        }).ToList();

        return Result<ProductPageDto>.Ok(new ProductPageDto(
            summaries, p, size, total,
            summaries.Count == 0
                ? total == 0 ? ReasonCodes.NoMatches : ReasonCodes.PageBeyondLast
                : null));
    }

    private static bool InRange(long amount, long? min, long? max)
        => (min is not { } lo || amount >= lo) && (max is not { } hi || amount <= hi);
}
