using Microsoft.EntityFrameworkCore;

namespace ECommerce.Shared.Messaging.Inbox;

/// <summary>
/// REL-003 — runs a business effect at most once per (message_id, consumer).
/// </summary>
/// <remarks>
/// The inbox row and the effect share one transaction, so either both land or neither does.
/// A duplicate delivery leaves state unchanged and does NOT raise an error, because
/// at-least-once delivery makes duplicates normal rather than exceptional.
/// </remarks>
public sealed class InboxDeduplicator(DbContext db)
{
    public async Task<bool> ExecuteOnceAsync(
        Guid messageId,
        string consumer,
        DateTimeOffset receivedAt,
        Func<CancellationToken, Task> effect,
        CancellationToken ct = default)
    {
        var alreadyHandled = await db.Set<InboxMessage>()
            .AnyAsync(m => m.MessageId == messageId && m.Consumer == consumer, ct);

        if (alreadyHandled) return false;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await effect(ct);
            db.Add(InboxMessage.Record(messageId, consumer, receivedAt));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsInboxKeyConflict(ex))
        {
            // Two instances consumed the same message concurrently (competing consumers under
            // FR-036 redundancy). The primary key on (message_id, consumer) is the arbiter;
            // losing that race is a duplicate, not a failure.
            //
            // Narrowly scoped on purpose: catching every DbUpdateException here would report a
            // FAILED EFFECT as a duplicate, the message would be acknowledged, and the effect
            // would be lost for good. Anything that is not the inbox key conflict propagates so
            // the transport can retry and, eventually, dead-letter it (REL-006).
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// True only for a unique-violation on the inbox key. SQLSTATE 23505 is read without taking
    /// a dependency on the provider, so this stays a technical primitive (MOD-003).
    /// A foreign-key violation is 23503 and must NOT be mistaken for a duplicate.
    /// </summary>
    private static bool IsInboxKeyConflict(DbUpdateException exception)
    {
        if (exception.Entries.Count > 0 &&
            exception.Entries.Any(e => e.Entity is not InboxMessage))
            return false;

        var sqlState = exception.InnerException?
            .GetType()
            .GetProperty("SqlState")?
            .GetValue(exception.InnerException) as string;

        return sqlState == "23505";
    }
}
