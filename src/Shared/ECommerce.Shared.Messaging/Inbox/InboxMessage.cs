namespace ECommerce.Shared.Messaging.Inbox;

/// <summary>
/// REL-003 — deduplication for at-least-once delivery, keyed on (message_id, consumer).
/// </summary>
/// <remarks>
/// The row is inserted in the SAME transaction as the business effect, which is what makes a
/// replayed message produce exactly one effect. Exactly-once delivery is not achievable;
/// at-least-once delivery plus this table is.
/// </remarks>
public sealed class InboxMessage
{
    private InboxMessage(Guid messageId, string consumer, DateTimeOffset receivedAt)
    {
        MessageId = messageId;
        Consumer = consumer;
        ReceivedAt = receivedAt;
    }

    private InboxMessage() { Consumer = null!; } // EF

    public Guid MessageId { get; private set; }
    public string Consumer { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    public static InboxMessage Record(Guid messageId, string consumer, DateTimeOffset receivedAt)
    {
        if (messageId == Guid.Empty)
            throw new ArgumentException("MSG-001 requires a message_id.", nameof(messageId));
        if (string.IsNullOrWhiteSpace(consumer))
            throw new ArgumentException("REL-003 keys on (message_id, consumer).", nameof(consumer));

        return new InboxMessage(messageId, consumer, receivedAt);
    }
}
