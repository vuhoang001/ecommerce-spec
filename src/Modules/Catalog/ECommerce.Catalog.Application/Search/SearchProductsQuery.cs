using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Domain;
using ECommerce.Shared.Kernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.Application.Search;

/// <summary>
/// US3 — FR-017, FR-018, FR-019, FR-020: partial name match, ignoring letter case and
/// diacritics in both directions.
/// </summary>
/// <remarks>
/// research.md R3: the stored name and the keyword pass through the same
/// <c>lower(immutable_unaccent(...))</c> normalisation, which is what makes the match work in
/// both directions from one GIN trigram index.
/// </remarks>
public sealed class SearchProductsQuery(DbContext db)
{
    public async Task<Result<ProductPageDto>> ExecuteAsync(
        string? keyword, int? page, int? pageSize, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Result<ProductPageDto>.Fail(
                ReasonCodes.EmptyKeyword,
                "A search keyword is required; an empty keyword does not return the catalogue.");

        var (p, size) = Paging.Normalise(page, pageSize);
        var normalised = keyword.Trim();

        // The stored name is normalised once, into the generated column; the keyword is
        // normalised by the same SQL function at query time. Both sides therefore pass through
        // identical logic, which is what makes FR-017 hold in both diacritic directions.
        var query = db.Set<Product>().Where(x =>
            EF.Functions.Like(
                EF.Property<string>(x, "NameNormalized"),
                "%" + CatalogFunctions.Normalise(normalised) + "%"));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
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

        var summaries = items.Select(x => new ProductSummaryDto(
            x.Id, x.Name, x.PrimaryImageUrl,
            new PriceDisplayDto(new MoneyDto(x.PriceMinor, x.Currency), null, false),
            x.StockQuantity == 0)).ToList();

        return Result<ProductPageDto>.Ok(new ProductPageDto(
            summaries, p, size, total,
            summaries.Count == 0
                ? total == 0 ? ReasonCodes.NoMatches : ReasonCodes.PageBeyondLast
                : null));
    }
}

/// <summary>
/// Maps to catalog.normalise_name(text), the IMMUTABLE wrapper the migrations create
/// (research.md R3). Callable only inside a query — EF translates it to SQL.
/// </summary>
public static class CatalogFunctions
{
    public static string Normalise(string value) =>
        throw new InvalidOperationException(
            "catalog.normalise_name is a database function; call it inside a LINQ query.");
}
