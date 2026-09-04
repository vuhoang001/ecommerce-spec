using ECommerce.Catalog.Application.Contracts;
using ECommerce.Catalog.Application.Pricing;
using ECommerce.Catalog.Domain;
using ECommerce.Shared.Kernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.Application.Detail;

/// <summary>
/// US2/AC1 — FR-009, FR-002: everything a customer needs to decide, at the product's list
/// price. Discount resolution is layered on in Phase 4B; this stands alone.
/// </summary>
public sealed class GetProductDetailQuery(DbContext db, ProductPriceResolver prices)
{
    public async Task<Result<ProductDetailDto>> ExecuteAsync(Guid productId, CancellationToken ct = default)
    {
        // The global query filter means a Hidden or Discontinued product simply is not here,
        // so it is reported identically to one that never existed (FR-002).
        var product = await db.Set<Product>()
            .Where(p => p.Id == productId)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.StockQuantity,
                PriceMinor = EF.Property<long>(p, "_priceMinor"),
                Currency = EF.Property<string>(p, "_currencyCode"),
                Categories = p.Categories
                    .OrderBy(c => c.Name)
                    .Select(c => new CategoryRefDto(c.Id, c.Name, c.Slug)).ToList(),
                Images = p.Images
                    .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Position)
                    .Select(i => new ProductImageDto(i.Url, i.Position)).ToList(),
                PrimaryImageUrl = p.Images
                    .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.Position)
                    .Select(i => i.Url).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (product is null)
            return Result<ProductDetailDto>.Fail(ReasonCodes.ProductNotFound, "No such product.");

        // FR-010, FR-012, FR-013, FR-015: live Promotion, then the discount copy, then the
        // list price. Catalog never calculates the discount itself (FR-011, PRM-001).
        var price = await prices.ResolveAsync(product.Id, product.PriceMinor, product.Currency, ct);

        return Result<ProductDetailDto>.Ok(new ProductDetailDto(
            product.Id,
            product.Name,
            product.PrimaryImageUrl,
            price,
            product.StockQuantity == 0,
            product.Description,
            product.StockQuantity,
            product.Categories,
            product.Images));
    }
}
