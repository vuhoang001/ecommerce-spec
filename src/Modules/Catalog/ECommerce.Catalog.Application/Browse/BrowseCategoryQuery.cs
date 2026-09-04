using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Domain;
using ECommerce.Shared.Kernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.Application.Browse;

/// <summary>
/// FR-003, FR-004, FR-005, FR-006, FR-007, FR-008 — a page of the Active products in one
/// category, with the total and the page position always stated.
/// </summary>
public sealed class BrowseCategoryQuery(DbContext db)
{
    public async Task<Result<ProductPageDto>> ExecuteAsync(
        Guid categoryId, int? page, int? pageSize, CancellationToken ct = default)
    {
        var (p, size) = Paging.Normalise(page, pageSize);

        var categoryExists = await db.Set<Category>().AnyAsync(c => c.Id == categoryId, ct);
        if (!categoryExists)
            return Result<ProductPageDto>.Fail(ReasonCodes.CategoryNotFound, "No such category.");

        // The global query filter keeps this to Active products (FR-001).
        var query = db.Set<Product>().Where(x => x.Categories.Any(c => c.Id == categoryId));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)   // default ordering: newest first
            .ThenBy(x => x.Id)                     // stable tiebreak so paging never repeats a row
            .Skip((p - 1) * size)
            .Take(size)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.StockQuantity,
                PriceMinor = EF.Property<long>(x, "_priceMinor"),
                Currency = EF.Property<string>(x, "_currencyCode"),
                PrimaryImageUrl = x.Images
                    .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Position)
                    .Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync(ct);

        var summaries = items
            .Select(x => new ProductSummaryDto(
                x.Id,
                x.Name,
                x.PrimaryImageUrl,
                new PriceDisplayDto(new MoneyDto(x.PriceMinor, x.Currency), null, false),
                x.StockQuantity == 0))
            .ToList();

        // FR-008: an empty page always states why it is empty; never an error.
        string? emptyReason = summaries.Count == 0
            ? total == 0 ? ReasonCodes.NoProductsInCategory : ReasonCodes.PageBeyondLast
            : null;

        return Result<ProductPageDto>.Ok(new ProductPageDto(summaries, p, size, total, emptyReason));
    }
}
