using ECommerce.Catalog.Application.Contracts;
using Dapper;
using ECommerce.Catalog.Application.Ports;
using ECommerce.Catalog.Application.Reads;
using ECommerce.Promotion.Contracts.V1;
using ECommerce.Shared.Kernel.Primitives;
using Microsoft.Extensions.Logging;

namespace ECommerce.Catalog.Application.Pricing;

/// <summary>
/// Resolves the price a customer sees: live Promotion, then the discount copy, then the
/// undiscounted price.
/// </summary>
/// <remarks>
/// FR-011 / PRM-001 [not adopted — see architecture-burndown.md BD-005] — nothing here CALCULATES a discount. It displays what Promotion returned,
/// or a copy of what Promotion previously returned. There is no arithmetic on a discount rate
/// anywhere in this type.
/// </remarks>
public sealed class ProductPriceResolver(
    ICatalogReadConnection connections,
    IPromotionPricingPort promotion,
    IClock clock,
    CatalogPricingOptions options,
    ILogger<ProductPriceResolver> logger)
{
    public async Task<PriceDisplayDto> ResolveAsync(
        Guid productId, long listPriceMinor, string currencyCode, CancellationToken ct = default)
    {
        var list = new MoneyDto(listPriceMinor, currencyCode);

        PricingResult result;
        try
        {
            result = await promotion.GetPricingAsync(productId, listPriceMinor, currencyCode, ct);
        }
        catch (Exception ex)
        {
            // FR-013: an unreachable Promotion never stops the page rendering (SC-008).
            logger.LogWarning(ex,
                "Promotion unreachable for {ProductId}; falling back to the discount copy (FR-013).",
                productId);
            return await FromCopyAsync(productId, list, ct);
        }

        switch (result.OutcomeCase)
        {
            case PricingResult.OutcomeOneofCase.Applied:
            {
                var discounted = Clamp(result.Applied.DiscountedPrice.AmountMinor);

                // OBS-001 / SC-009: every application is logged with product and promotion.
                logger.LogInformation(
                    "Discount applied to {ProductId} by {PromotionId}: {DiscountedMinor} {Currency}.",
                    productId, result.Applied.PromotionId, discounted, currencyCode);

                return new PriceDisplayDto(
                    new MoneyDto(discounted, currencyCode), list, IsOutOfDate: false);
            }

            case PricingResult.OutcomeOneofCase.Rejected:
                // FR-012 / SC-009: the reason is recorded, never shown to the shopper.
                logger.LogInformation(
                    "Discount rejected for {ProductId} by {PromotionId}: {ReasonCode} {ReasonDetail}.",
                    productId, result.Rejected.PromotionId,
                    result.Rejected.ReasonCode, result.Rejected.ReasonDetail);

                return new PriceDisplayDto(list, Original: null, IsOutOfDate: false);

            case PricingResult.OutcomeOneofCase.Unavailable:
            default:
                logger.LogWarning(
                    "Promotion unavailable for {ProductId}: {ReasonCode}; using the discount copy (FR-013).",
                    productId, result.Unavailable?.ReasonCode);

                return await FromCopyAsync(productId, list, ct);
        }
    }

    private async Task<PriceDisplayDto> FromCopyAsync(
        Guid productId, MoneyDto list, CancellationToken ct)
    {
        // DAT-004: a read, so it runs through Dapper. discount_projection is not a
        // visibility-governed table, so DAT-005's fragment does not apply to it.
        using var connection = await connections.OpenAsync(ct);
        var copy = await connection.QuerySingleOrDefaultAsync<DiscountCopyRow>(
            new CommandDefinition("""
                SELECT d.promotion_id            AS "PromotionId",
                       d.discounted_price_minor  AS "DiscountedPriceMinor",
                       d.retrieved_at            AS "RetrievedAt"
                FROM catalog.discount_projection d
                WHERE d.product_id = @productId
                """, new { productId }, cancellationToken: ct));

        // FR-015: past the staleness limit, and when no copy is held, the undiscounted price is
        // shown — still marked possibly out of date, because Promotion could not be consulted.
        if (copy is null || clock.UtcNow - copy.RetrievedAt > options.DiscountStalenessLimit)
        {
            logger.LogInformation(
                "No usable discount copy for {ProductId}; showing the list price marked out of date (FR-015).",
                productId);
            return new PriceDisplayDto(list, Original: null, IsOutOfDate: true);
        }

        var discounted = Clamp(copy.DiscountedPriceMinor);
        logger.LogInformation(
            "Discount copy used for {ProductId} from {PromotionId}, retrieved {RetrievedAt} (FR-013).",
            productId, copy.PromotionId, copy.RetrievedAt);

        return new PriceDisplayDto(
            new MoneyDto(discounted, list.CurrencyCode), list, IsOutOfDate: true);
    }

    /// <summary>FR-016 — a displayed discounted price is never below zero.</summary>
    private static long Clamp(long amountMinor) => Math.Max(0, amountMinor);
}

/// <summary>Flat row for the discount copy lookup (DAT-004).</summary>
public sealed class DiscountCopyRow
{
    public Guid PromotionId { get; init; }
    public long DiscountedPriceMinor { get; init; }
    public DateTimeOffset RetrievedAt { get; init; }
}
