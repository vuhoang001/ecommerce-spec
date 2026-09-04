# Catalog Messaging — Dead-Letter and Replay

**Satisfies**: `REL-006` — every queue MUST have a dead-letter queue and a documented replay
procedure.

Until this file existed, `MessagingSetup.cs` carried a comment asserting the procedure "lives in
the module runbook". It did not. A comment claiming compliance is worse than an acknowledged gap,
because it stops the next reader looking.

## What the catalog module consumes

| Queue | Message | Consumer | Dead-letter queue |
|---|---|---|---|
| `discount-changed-consumer` | `promotion.discount.changed.v1` | `catalog.discount-projection` | `discount-changed-consumer_error` |

The catalogue **publishes nothing**, so no outbox message can dead-letter today. The relay and
its tests exist so `REL-001` and `REL-002` are proven before the first publisher
(`plan.md` Complexity Tracking).

## How a message reaches the dead-letter queue

Three gates, in order:

1. **Immediate retry** — 3 attempts, no delay. Covers a momentary blip.
2. **Delayed redelivery** — after 1, 5 and 15 minutes. Waits out a transient outage: a database
   failing over, RabbitMQ restarting, Promotion unavailable.
3. **Dead-letter** — the message moves to `<queue>_error` and stays there. Nothing discards it.

A message that fails all three is not transient. Read it before replaying it.

## Why replay is safe

`REL-003` — every consumer deduplicates on `(message_id, consumer)` in an inbox row written in
the **same transaction** as the business effect. A replayed message is, by definition, a
duplicate: it produces exactly one effect and leaves state unchanged.

This is why replay is a routine operation here and not a risk. It is also why you must **not**
edit `message_id` when replaying — doing so defeats the deduplication and applies the effect a
second time.

## Procedure

### 1. See what is waiting

```bash
docker exec -it <rabbitmq> rabbitmqctl list_queues name messages | grep _error
```

### 2. Read one before replaying any

Open the RabbitMQ management UI at `:15672`, select `<queue>_error`, and **Get Message** with
*Requeue: Yes*. Look at the `MT-Fault-Message` and `MT-Fault-StackTrace` headers.

Decide which case you are in:

| Cause | Action |
|---|---|
| Transient — database was down, Promotion unreachable | Replay. It will now succeed. |
| Poison — malformed payload, a bug in the consumer | **Do not replay.** Fix the consumer, deploy, then replay. |
| Obsolete — a newer fact for the same product has already been applied | Do not replay. `REL-004` means the older fact would be ignored anyway; discard it deliberately and record why. |

### 3. Replay

```bash
# Move every message from the error queue back to its consumer queue.
docker exec -it <rabbitmq> rabbitmqctl shovel_start replay \
  --src-queue discount-changed-consumer_error \
  --dest-queue discount-changed-consumer
```

Without the shovel plugin, use the management UI: **Get Messages** with *Requeue: No*, then
publish each payload back to the consumer queue **preserving every header**, `message_id` above
all.

### 4. Confirm the effect landed

```sql
-- The inbox proves the message was handled; the projection proves what it did.
SELECT message_id, consumer, received_at
  FROM catalog.inbox_message
 ORDER BY received_at DESC LIMIT 10;

SELECT product_id, discounted_price_minor, occurred_at, retrieved_at
  FROM catalog.discount_projection
 ORDER BY retrieved_at DESC LIMIT 10;
```

A replayed message that was already handled adds **no** new inbox row and changes nothing. That
is the correct outcome, not a failed replay.

### 5. Record it

Add a line to the table below. An error queue that empties with no record is indistinguishable
from one somebody quietly purged.

## Replay log

| Date | Queue | Messages | Cause | Outcome | Operator |
|---|---|---|---|---|---|
| _none yet_ | | | | | |

## What is not covered

The dead-letter queue has **no alerting**. Nothing notices a message arriving there; someone has
to look. That is a genuine gap, and it belongs with the observability work rather than here —
`REL-006` requires the queue and the procedure, both of which now exist.
