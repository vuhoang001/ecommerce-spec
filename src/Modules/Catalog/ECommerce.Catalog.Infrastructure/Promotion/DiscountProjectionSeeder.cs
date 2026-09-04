using ECommerce.Catalog.Application.Ports;
using ECommerce.Catalog.Domain;
using ECommerce.Shared.Kernel;
using ECommerce.Shared.Kernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ECommerce.Catalog.Infrastructure.Promotion;

/// <summary>
/// FR-031 — seeds the discount copy at start-up so a product discounted before the catalogue
/// began running is present.
/// </summary>
/// <remarks>
/// research.md R12: under FR-036 redundancy several instances start together and would each run
/// a full seed. A PostgreSQL advisory lock means exactly one seeds. The seed itself is an
/// idempotent upsert, so losing the lock costs duplicated work rather than a wrong projection.
/// </remarks>
public sealed class DiscountProjectionSeeder(
    CatalogDbContext db,
    IPromotionPricingPort promotion,
    IClock clock,
    ILogger<DiscountProjectionSeeder> logger)
{
    /// <summary>Arbitrary but stable: every instance must contend for the same lock.</summary>
    private const long AdvisoryLockKey = 0x0CA7A106_D15C0117;

    private const int PageSize = 500;

    public async Task<int> SeedAsync(CancellationToken ct = default)
    {
        // A PostgreSQL advisory lock is SESSION-scoped. EF opens and closes connections per
        // operation, so the lock would be released the moment the connection returned to the
        // pool and every instance would seed. Pinning one connection for the whole seed is what
        // makes the lock mean anything (research.md R12).
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var acquired = await db.Database
                .SqlQuery<bool>($"SELECT pg_try_advisory_lock({AdvisoryLockKey}) AS \"Value\"")
                .SingleAsync(ct);

            if (!acquired)
            {
                logger.LogInformation(
                    "Discount copy seed skipped: another instance holds the advisory lock (FR-031).");
                return 0;
            }

            try
            {
                return await UpsertAsync(ct);
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync(
                    $"SELECT pg_advisory_unlock({AdvisoryLockKey})", ct);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task<int> UpsertAsync(CancellationToken ct)
    {
        var seeded = 0;
        var discounts = await promotion.ListActiveDiscountsAsync(PageSize, null, ct);

        foreach (var discount in discounts)
        {
            if (!Guid.TryParse(discount.ProductId, out var productId)) continue;
            if (!Guid.TryParse(discount.PromotionId, out var promotionId)) continue;

            var price = Money.FromMinor(
                discount.DiscountedPrice.AmountMinor, discount.DiscountedPrice.CurrencyCode);
            var occurredAt = discount.OccurredAt?.ToDateTimeOffset() ?? clock.UtcNow;

            var existing = await db.DiscountProjections
                .FirstOrDefaultAsync(d => d.ProductId == productId, ct);

            if (existing is null)
            {
                db.DiscountProjections.Add(DiscountProjection.Create(
                    productId, promotionId, price, occurredAt, clock.UtcNow));
            }
            else
            {
                existing.TryApply(promotionId, price, occurredAt, clock.UtcNow);
            }

            seeded++;
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        logger.LogInformation("Discount copy seeded {Count} product(s) (FR-031).", seeded);
        return seeded;
    }
}
