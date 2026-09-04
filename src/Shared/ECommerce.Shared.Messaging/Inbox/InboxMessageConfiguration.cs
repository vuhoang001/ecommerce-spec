using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Shared.Messaging.Inbox;

/// <summary>
/// Maps the inbox into the consuming module's own schema — the context's default schema — so
/// DAT-001 holds: each module owns its inbox table, nobody shares one.
/// </summary>
public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_message");
        builder.HasKey(m => new { m.MessageId, m.Consumer });

        builder.Property(m => m.MessageId).HasColumnName("message_id");
        builder.Property(m => m.Consumer).HasColumnName("consumer").HasMaxLength(200);
        builder.Property(m => m.ReceivedAt).HasColumnName("received_at").IsRequired();

        builder.HasIndex(m => m.ReceivedAt).HasDatabaseName("ix_inbox_message_received_at");
    }
}
