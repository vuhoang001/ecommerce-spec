namespace ECommerce.Promotion.Contracts.Events;

/// <summary>
/// <c>promotion.discount.changed.v1</c> — a past-tense fact, broadcast (MSG-002).
/// </summary>
/// <remarks>
/// Published by Promotion, consumed by Catalog to maintain its discount copy (research.md R1).
/// A breaking change ships as <c>.v2</c> alongside <c>.v1</c> until every consumer has migrated
/// (MSG-003). Consumers are tolerant readers: an added field must be ignored, not rejected
/// (REL-005).
/// </remarks>
public sealed record DiscountChangedV1
{
    public const string MessageType = "promotion.discount.changed.v1";

    // MSG-001 — the envelope every message carries.
    public required Guid MessageId { get; init; }
    public string Type { get; init; } = MessageType;
    public int Version { get; init; } = 1;
    public required DateTimeOffset OccurredAt { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid CausationId { get; init; }

    // Payload.
    public required Guid ProductId { get; init; }
    public required Guid PromotionId { get; init; }
    public required DiscountOutcome Outcome { get; init; }

    /// <summary>Present when <see cref="Outcome"/> is Applied. Integer minor units (MON-001).</summary>
    public long? DiscountedPriceMinor { get; init; }

    public string CurrencyCode { get; init; } = "VND";
}

public enum DiscountOutcome
{
    Applied = 0,

    /// <summary>The discount ended. The copy is removed and the product matches on list price (FR-027).</summary>
    Withdrawn = 1
}
