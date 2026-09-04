using ECommerce.Promotion.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ECommerce.Catalog.Infrastructure.Consumers;

/// <summary>
/// The transport-facing shell for <c>promotion.discount.changed.v1</c>. All behaviour —
/// envelope validation (COM-006), inbox deduplication (REL-003) and out-of-order tolerance
/// (REL-004) — lives in <see cref="DiscountChangedHandler"/>, which is testable without a
/// broker.
/// </summary>
public sealed class DiscountChangedConsumer(
    DiscountChangedHandler handler,
    ILogger<DiscountChangedConsumer> logger) : IConsumer<DiscountChangedV1>
{
    public async Task Consume(ConsumeContext<DiscountChangedV1> context)
    {
        var applied = await handler.HandleAsync(context.Message, context.CancellationToken);

        if (!applied)
        {
            // A duplicate leaves state unchanged and does NOT raise an error — at-least-once
            // delivery makes duplicates normal rather than exceptional (REL-003).
            logger.LogDebug("Duplicate {MessageId} ignored by {Consumer}.",
                context.Message.MessageId, DiscountChangedHandler.ConsumerName);
        }
    }
}
