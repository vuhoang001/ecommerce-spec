using ECommerce.Catalog.Domain;
using ECommerce.Promotion.Contracts.Events;
using ECommerce.Shared.Kernel;
using ECommerce.Shared.Kernel.Primitives;
using ECommerce.Shared.Messaging.Envelope;
using ECommerce.Shared.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Catalog.Infrastructure.Consumers;

/// <summary>
/// Maintains the discount copy from <c>promotion.discount.changed.v1</c> (FR-031).
/// </summary>
/// <remarks>
/// REL-003 — the inbox row is written in the same transaction as the projection change.
/// REL-004 — an update applies only when the incoming <c>occurred_at</c> is newer, so
/// reverse-order delivery converges to the same state.
/// REL-005 — the message type ignores unknown fields; nothing here rejects an added field.
/// MSG-001 — the envelope is validated before any effect runs.
/// </remarks>
public sealed class DiscountChangedHandler(CatalogDbContext db, IClock clock)
{
    public const string ConsumerName = "catalog.discount-projection";

    public async Task<bool> HandleAsync(DiscountChangedV1 message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        EnvelopeValidator.ThrowIfInvalid(new MessageEnvelope(
            message.MessageId, message.Type, message.Version,
            message.OccurredAt, message.CorrelationId, message.CausationId));

        var deduplicator = new InboxDeduplicator(db);

        return await deduplicator.ExecuteOnceAsync(
            message.MessageId, ConsumerName, clock.UtcNow,
            async token => await ApplyAsync(message, token), ct);
    }

    private async Task ApplyAsync(DiscountChangedV1 message, CancellationToken ct)
    {
        var existing = await db.DiscountProjections
            .FirstOrDefaultAsync(d => d.ProductId == message.ProductId, ct);

        if (message.Outcome == DiscountOutcome.Withdrawn)
        {
            // FR-027: with no copy the product matches on its original price alone.
            // Guard on occurred_at so a late Withdrawn cannot undo a newer Applied (REL-004).
            if (existing is not null && message.OccurredAt > existing.OccurredAt)
                db.DiscountProjections.Remove(existing);
            return;
        }

        if (message.DiscountedPriceMinor is not { } minor)
            throw new InvalidOperationException(
                $"{DiscountChangedV1.MessageType}: Applied carries no discounted price.");

        var price = Money.FromMinor(minor, message.CurrencyCode);

        if (existing is null)
        {
            db.DiscountProjections.Add(DiscountProjection.Create(
                message.ProductId, message.PromotionId, price, message.OccurredAt, clock.UtcNow));
            return;
        }

        existing.TryApply(message.PromotionId, price, message.OccurredAt, clock.UtcNow);
    }
}
