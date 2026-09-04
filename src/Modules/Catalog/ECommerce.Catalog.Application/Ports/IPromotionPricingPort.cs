using ECommerce.Promotion.Contracts.V1;

namespace ECommerce.Catalog.Application.Ports;

/// <summary>
/// The cross-module read Catalog needs from Promotion (COM-001).
/// </summary>
/// <remarks>
/// Declared here, in the CONSUMER, and implemented outside the domain — that ownership is what
/// the COM-001 architecture test asserts. It is deliberately READ-ONLY: there is no method that
/// changes anything in Promotion, which is PRM-001 [not adopted — see architecture-burndown.md BD-005] expressed in the type system rather than in
/// a comment. Transport is an in-process adapter today and a gRPC client after extraction
/// (research.md R5); neither this interface nor its callers change when that is swapped.
/// </remarks>
public interface IPromotionPricingPort
{
    /// <summary>Current pricing for one product. Used by the detail view (FR-010).</summary>
    Task<PricingResult> GetPricingAsync(Guid productId, long originalPriceMinor,
        string currencyCode, CancellationToken ct = default);

    /// <summary>
    /// Every currently active discount, paged. Seeds and reconciles the discount copy
    /// (research.md R1) — a filter must see products nobody has browsed to.
    /// </summary>
    Task<IReadOnlyList<AppliedDiscount>> ListActiveDiscountsAsync(
        int pageSize, string? pageToken, CancellationToken ct = default);
}
