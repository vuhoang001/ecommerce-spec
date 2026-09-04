namespace ECommerce.Shared.Messaging.Envelope;

/// <summary>
/// MSG-001 — a message missing any envelope field is rejected at the transport boundary.
/// </summary>
/// <remarks>
/// REL-003's inbox key is <c>(message_id, consumer)</c>, so deduplication is only possible
/// while <c>message_id</c> is guaranteed present. This validator is what guarantees it.
/// </remarks>
public static class EnvelopeValidator
{
    public sealed record Failure(string Field, string Reason);

    public static IReadOnlyList<Failure> Validate(MessageEnvelope? envelope)
    {
        if (envelope is null)
            return [new Failure("envelope", "The message carries no envelope.")];

        var failures = new List<Failure>();

        if (envelope.MessageId == Guid.Empty)
            failures.Add(new Failure("message_id", "Required; REL-003 deduplicates on it."));

        if (string.IsNullOrWhiteSpace(envelope.Type))
            failures.Add(new Failure("type", "Required."));

        if (envelope.Version <= 0)
            failures.Add(new Failure("version", "Required and positive; MSG-003 versions schemas."));

        if (envelope.OccurredAt == default)
            failures.Add(new Failure("occurred_at", "Required; REL-004 orders by it."));

        if (envelope.CorrelationId == Guid.Empty)
            failures.Add(new Failure("correlation_id", "Required; a failure must be traceable."));

        if (envelope.CausationId == Guid.Empty)
            failures.Add(new Failure("causation_id", "Required; a failure must be traceable."));

        return failures;
    }

    public static bool IsValid(MessageEnvelope? envelope) => Validate(envelope).Count == 0;

    public static void ThrowIfInvalid(MessageEnvelope? envelope)
    {
        var failures = Validate(envelope);
        if (failures.Count == 0) return;

        throw new InvalidOperationException(
            "MSG-001: message rejected at the transport boundary — " +
            string.Join("; ", failures.Select(f => $"{f.Field}: {f.Reason}")));
    }
}
