using ECommerce.Catalog.Application.Ports;
using ECommerce.Promotion.Contracts.V1;

namespace ECommerce.Catalog.ContractTests;

/// <summary>
/// A controllable Promotion (research.md R10). FR-013's unreachable behaviour and SC-008 can
/// only be tested by making Promotion fail on demand, which a real dependency will not do.
/// </summary>
public sealed class PromotionFake : IPromotionPricingPort
{
    public Func<Guid, PricingResult>? Pricing { get; set; }
    public bool Throw { get; set; }
    public List<AppliedDiscount> ActiveDiscounts { get; } = [];
    public int GetPricingCallCount { get; private set; }

    public Task<PricingResult> GetPricingAsync(
        Guid productId, long originalPriceMinor, string currencyCode, CancellationToken ct = default)
    {
        GetPricingCallCount++;

        if (Throw) throw new TimeoutException("Promotion is unreachable.");

        var result = Pricing?.Invoke(productId) ?? new PricingResult
        {
            Unavailable = new PricingUnavailable { ProductId = productId.ToString(), ReasonCode = "UNAVAILABLE" }
        };
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<AppliedDiscount>> ListActiveDiscountsAsync(
        int pageSize, string? pageToken, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AppliedDiscount>>(ActiveDiscounts);

    /// <summary>
    /// A fixed instant, deliberately. A pricing read is a pure function of its input (PRM-001),
    /// so stamping DateTimeOffset.UtcNow per call would make the fake non-deterministic and the
    /// purity test would be asserting the fake's clock rather than the port's contract.
    /// </summary>
    public static readonly DateTimeOffset FixedInstant = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    public static PricingResult Applied(Guid productId, Guid promotionId, long minor, string currency = "VND")
        => new()
        {
            Applied = new AppliedDiscount
            {
                ProductId = productId.ToString(),
                PromotionId = promotionId.ToString(),
                DiscountedPrice = new Money { AmountMinor = minor, CurrencyCode = currency },
                OccurredAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(FixedInstant)
            }
        };

    public static PricingResult Rejected(Guid productId, Guid promotionId, string reasonCode)
        => new()
        {
            Rejected = new RejectedDiscount
            {
                ProductId = productId.ToString(),
                PromotionId = promotionId.ToString(),
                ReasonCode = reasonCode,
                ReasonDetail = "Recorded, not shown to the shopper."
            }
        };

    public static PricingResult Unavailable(Guid productId, string reasonCode = "TIMEOUT")
        => new()
        {
            Unavailable = new PricingUnavailable { ProductId = productId.ToString(), ReasonCode = reasonCode }
        };
}
