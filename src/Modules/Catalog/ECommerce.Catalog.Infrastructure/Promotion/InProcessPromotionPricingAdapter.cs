using ECommerce.Catalog.Application.Ports;
using ECommerce.Promotion.Contracts.V1;
using Google.Protobuf.WellKnownTypes;

namespace ECommerce.Catalog.Infrastructure.Promotion;

/// <summary>
/// COM-001 — the port's implementation, outside the domain, over the proto-defined contract.
/// </summary>
/// <remarks>
/// research.md R5: inside one process a loopback gRPC call buys serialisation cost and no
/// isolation, because both modules already share a failure domain. This adapter calls the
/// Promotion module directly using the same generated message types; after extraction it is
/// replaced by a GrpcChannel client behind this identical port, and nothing else changes.
/// <para>
/// The Promotion module body is a later feature, so this adapter currently reports the module
/// as unavailable rather than pretending a discount exists. FR-013 makes that a supported
/// state, not a failure: the catalogue falls back to its discount copy and keeps rendering.
/// </para>
/// COM-002: this adapter makes no further cross-module call while serving one — call depth 1.
/// COM-003: it never enlists in the caller's database transaction.
/// PRM-001: read-only. There is no method here that changes anything in Promotion.
/// </remarks>
public sealed class InProcessPromotionPricingAdapter : IPromotionPricingPort
{
    public Task<PricingResult> GetPricingAsync(
        Guid productId, long originalPriceMinor, string currencyCode, CancellationToken ct = default)
    {
        var result = new PricingResult
        {
            Unavailable = new PricingUnavailable
            {
                ProductId = productId.ToString(),
                ReasonCode = "UNAVAILABLE"
            }
        };
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<AppliedDiscount>> ListActiveDiscountsAsync(
        int pageSize, string? pageToken, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AppliedDiscount>>([]);
}
