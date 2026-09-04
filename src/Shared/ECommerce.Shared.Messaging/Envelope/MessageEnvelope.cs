namespace ECommerce.Shared.Messaging.Envelope;

/// <summary>The six fields COM-006 requires on every message.</summary>
public sealed record MessageEnvelope(
    Guid MessageId,
    string Type,
    int Version,
    DateTimeOffset OccurredAt,
    Guid CorrelationId,
    Guid CausationId);
