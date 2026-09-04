using ECommerce.Shared.Kernel;

namespace ECommerce.Catalog.Domain;

/// <summary>
/// Catalog's own copy of the currently active discount for a product — the spec's
/// <em>discount copy</em> (FR-014).
/// </summary>
/// <remarks>
/// research.md R1: FR-026 requires filtering on the discounted price, and DAT-001 forbids
/// Catalog from reading Promotion's tables. A local, filterable copy is the only shape that
/// satisfies both. It is held for EVERY discounted product, not only viewed ones, because a
/// filter must see products nobody has browsed to.
/// <para>
/// Never authoritative. Promotion remains the owner; this row carries the moment it arrived so
/// FR-015 can refuse to show it once it is too old.
/// </para>
/// </remarks>
public sealed class DiscountProjection
{
    private long _discountedPriceMinor;
    private string _currencyCode = "VND";

    private DiscountProjection(Guid productId, Guid promotionId, Money discountedPrice,
        DateTimeOffset occurredAt, DateTimeOffset retrievedAt)
    {
        ProductId = productId;
        PromotionId = promotionId;
        _discountedPriceMinor = discountedPrice.AmountMinor;
        _currencyCode = discountedPrice.CurrencyCode;
        OccurredAt = occurredAt;
        RetrievedAt = retrievedAt;
    }

    private DiscountProjection() { } // EF

    public Guid ProductId { get; private set; }
    public Guid PromotionId { get; private set; }

    public Money DiscountedPrice => Money.FromMinor(_discountedPriceMinor, _currencyCode);

    /// <summary>From the source event. The ordering key that makes REL-004 safe.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>When Catalog stored it. Drives the FR-015 staleness limit.</summary>
    public DateTimeOffset RetrievedAt { get; private set; }

    public static DiscountProjection Create(Guid productId, Guid promotionId, Money discountedPrice,
        DateTimeOffset occurredAt, DateTimeOffset retrievedAt)
    {
        if (discountedPrice.IsNegative)
            throw new ArgumentOutOfRangeException(nameof(discountedPrice),
                discountedPrice.AmountMinor, "A discounted price cannot be negative (FR-016).");

        return new DiscountProjection(productId, promotionId, discountedPrice, occurredAt, retrievedAt);
    }

    /// <summary>
    /// REL-004: handlers must not assume ordering, so an update applies only when the incoming
    /// fact is newer than the stored one. Reverse delivery converges to the same state.
    /// </summary>
    public bool TryApply(Guid promotionId, Money discountedPrice,
        DateTimeOffset occurredAt, DateTimeOffset retrievedAt)
    {
        if (occurredAt <= OccurredAt) return false;

        if (discountedPrice.IsNegative)
            throw new ArgumentOutOfRangeException(nameof(discountedPrice),
                discountedPrice.AmountMinor, "A discounted price cannot be negative (FR-016).");

        PromotionId = promotionId;
        _discountedPriceMinor = discountedPrice.AmountMinor;
        _currencyCode = discountedPrice.CurrencyCode;
        OccurredAt = occurredAt;
        RetrievedAt = retrievedAt;
        return true;
    }

    /// <summary>FR-015: past the limit the copy is not shown and not used by the filter.</summary>
    public bool IsFresh(DateTimeOffset now, TimeSpan stalenessLimit) =>
        now - RetrievedAt <= stalenessLimit;
}
