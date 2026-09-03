<!--
SYNC IMPACT REPORT
==================
Document: specs/constitution.md (Master Architecture Constitution)
Version: 2.0.0
Previous: .specify/memory/constitution.md v1.0.1

Bump rationale: MAJOR. This document relocates, restructures, and materially redefines the
project constitution. Governance moves from a 7-principle narrative to an addressable rule
framework (stable rule IDs, conformance levels, waiver process, extension mechanism).
Existing obligations are preserved but re-expressed as citable rules, so every prior rule
number changes. That is a backward-incompatible governance change.

Carried forward from v1.0.1 (re-expressed, not weakened):
  I.   Bounded Context Boundaries & Modular Monolith  -> Article I (ARC-001..ARC-024)
  II.  Data Integrity & Idempotency                   -> Article III + Article VI
  III. Security & Compliance by Default               -> Article IX (SEC-*)
  IV.  API Contract Discipline                        -> Article II (COM-*)
  V.   Test-First for Money & Inventory Paths         -> Article X (QAG-*)
  VI.  End-to-End Observability                       -> Article VIII (OBS-*)
  VII. Fault Tolerance & Controlled Degradation       -> Article V (RES-*)

Added articles: III (Reliability/Outbox+Inbox), V (Resilience), VI (Saga), VII (Business
Edge Cases), XI (Extension & Custom Rules).

Deferred TODOs:
  - TODO(PROJECT_NAME): official commercial name not yet supplied; title reads
    "E-Commerce Platform". Update when settled (PATCH bump).
  - TODO(RATIFICATION_DATE): 2026-09-03 recorded from the working session that adopted
    this document. Correct if the project adopted these principles earlier.
-->

# E-Commerce Platform — Master Architecture Constitution

<!-- TODO(PROJECT_NAME): replace with the platform's official commercial name. -->

**Version**: 2.0.0 | **Ratified**: 2026-09-03 | **Last Amended**: 2026-09-03
**Status**: Active | **Supersedes**: `.specify/memory/constitution.md` v1.0.1

---

## 0. How To Read This Document

### 0.1 Authority

This constitution is the **single source of architectural truth**. It supersedes every other
convention, habit, tutorial, blog post, and AI-generated suggestion in this repository. Where a
spec, plan, task, pull request, or code review conflicts with this document, **this document
wins**. Changing that outcome requires amending this document first (§ Governance).

Every artifact — human-written or agent-generated — is in scope: specs, plans, tasks, source
code, tests, migrations, infrastructure-as-code, CI pipelines, and runbooks.

### 0.2 Requirement Levels (RFC 2119 / RFC 8174)

The key words **MUST**, **MUST NOT**, **REQUIRED**, **SHALL**, **SHALL NOT**, **SHOULD**,
**SHOULD NOT**, **RECOMMENDED**, **MAY**, and **OPTIONAL** are to be interpreted as described in
RFC 2119 and RFC 8174, and appear in **bold uppercase** when carrying that meaning.

| Level | Meaning in this project | Enforcement |
|---|---|---|
| **MUST** / **MUST NOT** | Absolute requirement. Non-compliance blocks merge. | CI gate + reviewer block |
| **SHOULD** / **SHOULD NOT** | Strong default. Deviation requires a written justification in the PR description. | Reviewer judgement |
| **MAY** / **OPTIONAL** | Genuinely free choice. No justification needed. | None |

A reviewer **MAY** block a merge citing a **MUST** violation alone, with no further argument
required beyond the rule ID.

### 0.3 Rule Identifiers

Every normative rule carries a stable identifier: `<PREFIX>-<NNN>`.

| Prefix | Article | Domain |
|---|---|---|
| `ARC` | I | Architectural principles, layering, isolation |
| `COM` | II | Communication standards (sync + async) |
| `REL` | III | Data reliability — Outbox, Inbox, idempotency |
| `TXN` | IV | Transaction boundaries & consistency model |
| `RES` | V | Resilience — retry, circuit breaker, DLQ |
| `SAG` | VI | Saga / distributed transactions |
| `EDG` | VII | Business edge cases — overselling, payment |
| `OBS` | VIII | Observability |
| `SEC` | IX | Security & compliance |
| `QAG` | X | Quality gates & testing |
| `EXT` | XI | Project-specific custom rules |

**Rule IDs are immutable.** A rule is never renumbered and never reused. A withdrawn rule is
marked `[WITHDRAWN in vX.Y.Z]` and left in place so historical review comments stay meaningful.

**Reserved ranges:**
- `001`–`899` — core constitution. Amended only through the § Governance process.
- `900`–`999` — **reserved for your custom rules** (see Article XI). Core amendments will never
  allocate into this range, so custom rules can never collide with an upstream change.

### 0.4 Citing Rules

In PR reviews, specs, and ADRs, cite the identifier directly:

> Blocking: this handler publishes to RabbitMQ inside the same `try` as the `SaveChangesAsync`
> call, which violates **REL-002** (atomicity of state change and event emission).

### 0.5 Technology Baseline

This constitution is written against a concrete stack. Rules are expressed in terms of
**capabilities**, not vendors, so the stack **MAY** be substituted — but any substitution
**MUST** preserve every capability listed here and **MUST** be recorded as an ADR.

| Concern | Baseline choice | Required capability |
|---|---|---|
| Runtime | .NET 8 (C# 12) | — |
| Relational store | PostgreSQL 16 | ACID transactions, `SELECT … FOR UPDATE SKIP LOCKED`, `JSONB` |
| ORM / persistence | EF Core 8 | Transaction control, interceptors, migrations |
| Message broker | RabbitMQ (default) or Kafka | At-least-once delivery, DLQ/DLX, consumer groups |
| Messaging framework | MassTransit | Outbox integration, retry policies, DLQ routing |
| Cache / distributed state | Redis 7 | Atomic ops, TTL, Lua scripting |
| Internal RPC | gRPC (HTTP/2, Protobuf) | Deadlines, streaming, code generation |
| Tracing | OpenTelemetry | W3C Trace Context propagation |

---

## Article I — Architectural Principles (`ARC`)

### I.1 Clean Architecture Layering

**ARC-001** — Every service and every module **MUST** be organised into exactly four layers:
`Domain`, `Application`, `Infrastructure`, `Presentation`.

**ARC-002** — The **Dependency Rule** is absolute: source-code dependencies **MUST** point only
inward, toward the Domain. No exceptions, no "temporary" shortcuts.

```
        ┌─────────────────────────────────────────────┐
        │  Presentation   (REST API, gRPC, Consumers) │
        │        ↓ depends on                         │
        │  Application    (Use cases, CQRS handlers)  │
        │        ↓ depends on                         │
        │  Domain         (Entities, VOs, Events)     │
        │                                             │
        │  Infrastructure ──→ implements Application  │
        │  (EF Core, broker, Redis, HTTP clients)     │
        │   depends inward; nothing depends on it     │
        └─────────────────────────────────────────────┘
```

**ARC-003** — The permitted dependency matrix is exhaustive. Any arrow not listed **MUST NOT**
exist:

| Layer | MAY reference | MUST NOT reference |
|---|---|---|
| `Domain` | Nothing but the BCL | Application, Infrastructure, Presentation, EF Core, MassTransit, ASP.NET, any NuGet package with I/O |
| `Application` | `Domain` | Infrastructure, Presentation, EF Core, any concrete broker/HTTP/DB type |
| `Infrastructure` | `Application`, `Domain` | Presentation |
| `Presentation` | `Application`, `Domain` | Other services' internals |

**ARC-004** — The `Domain` layer **MUST** be free of I/O. It **MUST NOT** perform database
access, HTTP calls, message publishing, file access, clock reads, or random-number generation
directly. Time and identity **MUST** be injected as abstractions (`IClock`, `IIdGenerator`).

*Rationale*: a domain that reads the clock cannot be tested deterministically, and a domain that
performs I/O cannot be reasoned about without a running environment.

**ARC-005** — The `Domain` layer **MUST NOT** carry persistence annotations (EF Core attributes,
`[Table]`, `[Column]`, serialization attributes). Mapping **MUST** live in
`Infrastructure/Persistence/Configurations/` using `IEntityTypeConfiguration<T>`.

**ARC-006** — Ports (interfaces) **MUST** be declared in the layer that *consumes* them, and
adapters (implementations) in `Infrastructure`. `IOrderRepository` is declared in `Application`
(or `Domain` where it is a domain concept) and implemented in `Infrastructure`.

**ARC-007** — Every service **MUST** ship an automated architecture test suite that fails the
build on a layering violation. Manual review is not sufficient enforcement.

```csharp
// tests/…/ArchitectureRulesTests.cs — enforcing ARC-003 / ARC-004
[Fact]
public void Domain_must_not_depend_on_outer_layers()
{
    var result = Types.InAssembly(typeof(DomainAssemblyMarker).Assembly)
        .ShouldNot()
        .HaveDependencyOnAny(
            "Microsoft.EntityFrameworkCore",
            "MassTransit",
            "Microsoft.AspNetCore",
            "StackExchange.Redis",
            "System.Net.Http")
        .GetResult();

    result.IsSuccessful.Should().BeTrue(
        "ARC-004: Domain must contain no I/O. Offenders: {0}",
        string.Join(", ", result.FailingTypeNames ?? []));
}
```

### I.2 Domain Isolation

**ARC-010** — Each Bounded Context **MUST** own its domain model exclusively. A type defined in
one context's `Domain` **MUST NOT** be referenced, imported, or project-referenced by another
context — not even for "just reading a field".

**ARC-011** — Cross-context communication **MUST** occur only through published contracts: gRPC
service definitions in `contracts/proto/` or integration event schemas in `contracts/events/`.
Contracts are the *only* legitimate coupling surface between contexts.

**ARC-012** — A context **MUST NOT** share an internal type by extracting it into a "Common" or
"Shared" library to dodge ARC-010. Shared libraries in `src/BuildingBlocks/` **MUST** contain
only technical primitives (base classes, messaging plumbing, result types) and **MUST NOT**
contain business concepts belonging to any single context.

*Rationale*: a shared `Product` class is how a modular monolith silently becomes a distributed
monolith. Once two contexts compile against the same business type, they can never deploy
independently.

**ARC-013** — Identical business concepts appearing in multiple contexts **MUST** be modelled
separately per context. `Ordering.Customer` and `Identity.User` are distinct types with distinct
invariants, correlated only by identifier. Duplication across contexts is **correct**, not a
defect to be refactored away.

**ARC-014** — Every context **MUST** publish a translation layer (Anti-Corruption Layer) at its
boundary. Inbound external data **MUST** be mapped into local domain types before reaching the
`Domain` layer. External DTOs **MUST NOT** leak inward past `Application`.

### I.3 Database-per-Service

**ARC-020** — Each Bounded Context **MUST** own its persistent data exclusively. No other
context **MAY** read or write its tables — not via SQL, not via a shared `DbContext`, not via a
read-only replica, not via a database view.

**ARC-021** — In the modular-monolith deployment, contexts **MUST** be separated at minimum by
**PostgreSQL schema** (`identity.*`, `catalog.*`, `ordering.*`, …), each with its own
`DbContext` and its own migration history table. This makes later physical extraction a
configuration change rather than a rewrite.

**ARC-022** — Foreign keys **MUST NOT** cross context boundaries. A cross-context reference
**MUST** be stored as a bare identifier (`CustomerId`), with referential integrity enforced by
the owning context, not by the database.

**ARC-023** — Cross-context `JOIN`s **MUST NOT** be written. Data needed from another context
**MUST** arrive by one of: (a) a synchronous query over its published contract, or (b) a local
read model kept current by integration events.

**ARC-024** — Extracting a module into a standalone microservice is permitted **ONLY** with
recorded evidence of an independent scaling or isolation need. The evidence **MUST** be captured
in an ADR before extraction begins. "It feels cleaner" is not evidence.

### I.4 CQRS

**ARC-030** — Every context **MUST** separate Commands from Queries. A Command mutates state and
**MUST NOT** return domain data beyond an identifier and an operation result. A Query reads state
and **MUST NOT** mutate anything.

**ARC-031** — Commands **MUST** be executed through the Aggregate Root, so invariants are
enforced in exactly one place. A Command handler **MUST NOT** manipulate persistence rows
directly to bypass an aggregate.

**ARC-032** — Queries **MAY** bypass the domain model entirely and project straight from the
database (Dapper, EF Core projections, dedicated read models). Loading a full aggregate to
service a read is discouraged and **SHOULD** be avoided on hot paths.

**ARC-033** — Where a read model is denormalized from integration events, the spec **MUST** state
its consistency window (for example, "eventually consistent, target p99 < 2s") and the UI
**MUST** be designed to tolerate that window.

### I.5 Statelessness

**ARC-040** — Every service **MUST** be strictly stateless. Application memory **MUST NOT** hold
any state that survives a request or that another instance would need: no in-process session
store, no static mutable caches of business data, no in-memory rate-limit counters, no scheduled
work tracked only in RAM.

**ARC-041** — Distributed state — sessions, rate-limit counters, locks, reservation holds,
idempotency records — **MUST** live in Redis or the database.

**ARC-042** — Any instance **MUST** be killable at any moment without data loss or duplicated
side effects. A deployment **MUST NOT** depend on graceful shutdown to remain correct; graceful
shutdown is an optimisation, not a correctness mechanism.

**ARC-043** — In-process caching of *immutable or slow-moving reference data* is permitted
(`MAY`), provided the cache is rebuildable from the source of truth on startup and staleness is
bounded by an explicit TTL.

---

## Article II — Communication Standards (`COM`)

### II.1 Choosing a Communication Style

**COM-001** — The communication style **MUST** be selected by this decision table. The choice
**MUST** be recorded in the feature spec, not left implicit in code.

| Situation | Required style | Rule |
|---|---|---|
| Client (web/mobile/3rd-party) → platform | **REST/JSON** over HTTPS | COM-010 |
| Internal, caller needs data *now* to proceed | **gRPC** (unary, read-only) | COM-020 |
| Internal, caller changes another context's state | **Integration event** — sync write **MUST NOT** be used | COM-030 |
| Internal, caller does not need the result | **Integration event** | COM-030 |
| Bulk/streaming internal read | **gRPC server streaming** | COM-021 |
| Long-running multi-context business transaction | **Saga** (Article VI) | SAG-001 |

**COM-002** — Synchronous coupling **MUST** be minimised. Before adding a synchronous call
between contexts, the author **MUST** demonstrate in the spec that eventual consistency is
unacceptable for that specific interaction.

### II.2 REST — External Edge

**COM-010** — Public HTTP APIs **MUST** be resource-oriented, **MUST** be versioned in the path
(`/api/v1/orders`), and **MUST** be served over TLS. Plain-HTTP endpoints **MUST NOT** exist in
any environment reachable outside localhost.

**COM-011** — REST is the **edge protocol only**. Internal service-to-service calls **MUST NOT**
use REST/JSON where gRPC is available; the exception is a third-party integration that offers no
gRPC surface.

**COM-012** — Every error response **MUST** use one platform-wide envelope, and **MUST** carry
the correlation identifier so a user-reported failure is traceable from a screenshot.

```jsonc
// HTTP 409
{
  "type":    "https://errors.example.com/inventory/insufficient-stock",
  "title":   "Insufficient stock",
  "status":  409,
  "code":    "INVENTORY_INSUFFICIENT_STOCK",   // stable, machine-readable, never localized
  "detail":  "Requested 5 units of SKU-1234; 2 available.",
  "instance":"/api/v1/orders",
  "correlationId": "01JBQ8F5X0K3M9WZ2N7YQH4T6V",
  "errors": [                                   // present only for validation failures
    { "field": "items[0].quantity", "code": "EXCEEDS_AVAILABLE", "message": "Max 2." }
  ]
}
```

**COM-013** — `code` values **MUST** be stable and machine-readable. Clients **MUST NOT** be
required to parse `detail` or `title`; those strings **MAY** change or be localized at any time.

**COM-014** — Every collection endpoint **MUST** paginate. Unbounded result sets **MUST NOT** be
returned. Cursor-based pagination is **RECOMMENDED** over offset for large or high-churn
collections.

**COM-015** — Breaking changes to a published REST contract **MUST** ship as a new version path.
A breaking change is: removing or renaming a field, narrowing a type, adding a required request
field, changing an existing `code`, or altering the meaning of an existing value.

**COM-016** — Inbound webhooks (payment gateways, logistics partners) **MUST** have their
signature cryptographically verified before *any* part of the payload is parsed or trusted, and
**MUST** be idempotent per REL-030 — gateways retry aggressively and will deliver duplicates.

### II.3 gRPC — Internal Synchronous Reads

**COM-020** — Internal synchronous **read** operations **MUST** use gRPC. Protobuf definitions
**MUST** live in `contracts/proto/<context>/v1/` and **MUST** be the generation source for both
server and client — hand-written clients **MUST NOT** be maintained in parallel.

**COM-021** — Bulk internal reads **SHOULD** use server streaming rather than paginated unary
calls.

**COM-022** — Every gRPC call **MUST** set an explicit deadline. A call without a deadline is a
resource leak under load and **MUST** fail code review.

```csharp
// COM-022 + RES-020: deadline is mandatory; it also bounds the circuit breaker's view of health
var reply = await _pricingClient.CalculateAsync(
    request,
    deadline: DateTime.UtcNow.AddMilliseconds(300),
    cancellationToken: ct);
```

**COM-023** — Internal synchronous calls **MUST NOT** exceed a chain depth of **2** (A → B → C).
A required depth of 3 or more indicates a missing read model or a misplaced boundary, and
**MUST** be resolved by introducing one, not by adding a hop.

**COM-024** — A gRPC method **MUST NOT** mutate state in another context. Cross-context writes go
through events (COM-030). A gRPC service exposing `CreateX` / `UpdateX` to another context is a
constitutional violation.

**COM-025** — Protobuf schema evolution **MUST** follow the wire-compatibility rules: field
numbers are never reused, fields are never renumbered, removed fields are `reserved`, and new
fields are optional with sane zero-value semantics.

```protobuf
// contracts/proto/inventory/v1/availability.proto
syntax = "proto3";
package inventory.v1;

message AvailabilityRequest {
  reserved 3;                       // COM-025: field 3 withdrawn in v1.4, never reuse
  reserved "warehouse_code";
  string sku       = 1;
  int32  quantity  = 2;
  string region_id = 4;             // added v1.5 — optional, zero value = "any region"
}
```

### II.4 Asynchronous Messaging — Event-Driven

**COM-030** — A state change in one context that another context must react to **MUST** be
communicated by an **Integration Event** published to the message broker. Direct synchronous
writes across contexts **MUST NOT** be used.

**COM-031** — Every significant state transition **MUST** emit a domain event, which **MUST** be
translated to an integration event at the boundary if other contexts need it. Polling another
context to detect change **MUST NOT** be used.

**COM-032** — Domain events and integration events **MUST** be distinct types. Domain events are
internal, fine-grained, and free to change; integration events are published contracts, coarse,
versioned, and governed by `specs/event-governance.md`. A domain event **MUST NOT** be published
to the broker directly.

**COM-033** — Integration events **MUST** be named `<context>.<aggregate>.<past-tense-verb>.v<N>`,
lowercase, dot-separated. Examples: `ordering.order.placed.v1`,
`payment.payment.authorized.v2`, `inventory.stock.reserved.v1`.

**COM-034** — Events **MUST** describe facts that have already happened, in the past tense. An
event **MUST NOT** be used to issue a command to a specific consumer ("`SendEmailCommand`" is not
an event). Commands are point-to-point and addressed; events are broadcast and unaddressed.

**COM-035** — A publisher **MUST NOT** know its consumers. If a change to publisher code is
required whenever a consumer is added, the coupling is wrong and **MUST** be corrected.

**COM-036** — Event payloads **MUST** be self-contained for their stated purpose: they **MUST**
carry the data a consumer needs to act. A consumer forced to immediately call back to the
publisher for basic context indicates an under-specified event, which **SHOULD** be enriched
rather than compensated for with a callback.

### II.5 The Contracts Directory

**COM-040** — All cross-context contracts **MUST** live in a single top-level `contracts/`
directory, versioned with the repository:

```
contracts/
├── proto/                          # gRPC service + message definitions
│   ├── identity/v1/…
│   ├── catalog/v1/…
│   └── inventory/v1/availability.proto
├── events/                         # Integration event schemas (JSON Schema)
│   ├── ordering/
│   │   ├── order.placed.v1.json
│   │   └── order.cancelled.v1.json
│   └── payment/
│       └── payment.authorized.v1.json
└── README.md                       # ownership map + change procedure
```

**COM-041** — A contract change **MUST** be reviewed by the owning context's maintainer *and* by
at least one consuming context's maintainer. Contract PRs **MUST** be separable from
implementation PRs so consumers can react before the producer ships.

**COM-042** — CI **MUST** fail on a backward-incompatible contract change that is not accompanied
by a version increment. Compatibility checking **MUST** be automated (`buf breaking` for
Protobuf, JSON Schema diff for events); reviewer vigilance is not an acceptable control.

---

## Article III — Data Reliability: Outbox & Inbox (`REL`)

> This article is **NON-NEGOTIABLE**. Every rule here is **MUST**. It exists because the naive
> implementation — save to the database, then publish to the broker — is silently broken: a crash
> between the two loses the event forever, and a broker acknowledgement lost in transit
> duplicates it. There is no correct dual-write. There is only the Outbox.

### III.1 The Dual-Write Problem

**REL-001** — A service **MUST NOT** write to its database and publish to the message broker as
two independent operations. The following pattern is **forbidden** and **MUST** fail code review:

```csharp
// ❌ FORBIDDEN — violates REL-001, REL-002
await _db.SaveChangesAsync(ct);                  // committed
await _bus.Publish(new OrderPlaced(order.Id));   // crash here ⇒ event lost forever
```

Failure modes, all of which occur in production:
- Process crashes between the two calls → **state changed, event never published**.
- Broker is unavailable → the write is committed but the publish throws; wrapping it in a retry
  merely widens the window.
- The publish succeeds but the acknowledgement is lost → the application retries → **duplicate
  event**.
- The developer reorders to publish-then-save → **event published for a state change that then
  rolls back**, which is worse.

### III.2 Transactional Outbox — Publisher Side

**REL-002** — A state change and the emission of its integration events **MUST** be committed in
a **single local ACID transaction**. The event **MUST** be written to an `outbox_messages` table
in the same database, in the same transaction as the business data.

```csharp
// ✅ REQUIRED — REL-002: one transaction, two writes, atomic together
await using var tx = await _db.Database.BeginTransactionAsync(ct);

var order = Order.Place(customerId, items, _clock.UtcNow);
_db.Orders.Add(order);                                   // business state
_outbox.Enqueue(order.DequeueIntegrationEvents());       // events, same DbContext

await _db.SaveChangesAsync(ct);                          // both rows or neither
await tx.CommitAsync(ct);
// Nothing has been published yet. The relay does that, separately and reliably.
```

**REL-003** — The outbox table **MUST** carry at minimum the following columns. Additional
columns **MAY** be added; none of these **MAY** be omitted.

```sql
-- REL-003: canonical outbox schema (PostgreSQL)
CREATE TABLE ordering.outbox_messages (
    id                UUID         PRIMARY KEY,       -- == envelope messageId; the dedup key
    aggregate_type    VARCHAR(100) NOT NULL,          -- 'Order'
    aggregate_id      VARCHAR(100) NOT NULL,          -- partition key for ordering (REL-006)
    event_type        VARCHAR(200) NOT NULL,          -- 'ordering.order.placed.v1'
    schema_version    INT          NOT NULL,
    payload           JSONB        NOT NULL,
    headers           JSONB        NOT NULL,          -- correlationId, causationId, traceparent
    occurred_at       TIMESTAMPTZ  NOT NULL,          -- business time
    status            SMALLINT     NOT NULL DEFAULT 0,-- 0=Pending 1=Published 2=Dead
    attempt_count     INT          NOT NULL DEFAULT 0,
    next_attempt_at   TIMESTAMPTZ  NOT NULL DEFAULT now(),
    published_at      TIMESTAMPTZ  NULL,
    last_error        TEXT         NULL
);

-- Dispatch scan: partial index keeps it small regardless of table size
CREATE INDEX ix_outbox_dispatch
    ON ordering.outbox_messages (next_attempt_at, id)
    WHERE status = 0;

-- Per-aggregate ordering (REL-006)
CREATE INDEX ix_outbox_ordering
    ON ordering.outbox_messages (aggregate_type, aggregate_id, occurred_at)
    WHERE status = 0;
```

**REL-004** — Publishing to the broker **MUST** be performed by a **separate relay process**
reading the outbox — never by the request thread that wrote the row. The relay **MUST** be
implemented as either:
- **(a) Polling publisher** — periodically scans `status = 0 AND next_attempt_at <= now()`; or
- **(b) Transaction-log tailing (CDC)** — Debezium or equivalent streaming the outbox table.

Option (a) is the **RECOMMENDED** default; (b) **SHOULD** be adopted only when measured
throughput justifies the operational cost.

**REL-005** — The polling relay **MUST** claim rows with `FOR UPDATE SKIP LOCKED` so multiple
instances can run concurrently without double-publishing or blocking each other. A relay that
takes a global lock or runs single-instance **MUST NOT** be deployed — it is a single point of
failure and a throughput ceiling.

```sql
-- REL-005: safe concurrent claim. SKIP LOCKED is what makes horizontal scaling correct.
WITH claimed AS (
    SELECT id
      FROM ordering.outbox_messages
     WHERE status = 0
       AND next_attempt_at <= now()
     ORDER BY occurred_at
     LIMIT 100
     FOR UPDATE SKIP LOCKED
)
UPDATE ordering.outbox_messages m
   SET attempt_count = m.attempt_count + 1
  FROM claimed c
 WHERE m.id = c.id
RETURNING m.*;
```

**REL-006** — Where a consumer depends on the **order** of events for one aggregate, the relay
**MUST** preserve per-aggregate ordering: dispatch **MUST** be ordered by `occurred_at` within
an `aggregate_id`, and the broker partition/routing key **MUST** be derived from `aggregate_id`.
Global ordering across aggregates **MUST NOT** be assumed by any consumer.

**REL-007** — The relay **MUST** mark a row `Published` only *after* the broker confirms receipt
(publisher confirms on RabbitMQ, `acks=all` on Kafka). Fire-and-forget publishing **MUST NOT**
be used.

**REL-008** — Because a crash can occur between broker acknowledgement and the local status
update, the outbox guarantees **at-least-once** delivery, never exactly-once. Every consumer
**MUST** therefore be idempotent (REL-020). No design **MAY** assume exactly-once delivery;
exactly-once does not exist across a network boundary.

**REL-009** — A message that has exhausted its retry budget (RES-011) **MUST** be marked
`status = 2 (Dead)`, **MUST NOT** be retried further automatically, and **MUST** raise an alert.
Dead outbox rows **MUST NOT** be deleted; they are the forensic record of what was lost.

**REL-010** — Published rows **MUST** be purged on a schedule (retention **RECOMMENDED** at 7–30
days). Purging **MUST NOT** delete `Pending` or `Dead` rows. An unpurged outbox degrades write
performance across the entire context.

**REL-011** — The relay **MUST** expose metrics: outbox depth (pending count), oldest pending age,
publish rate, failure rate, dead count. Alerts **MUST** fire on *oldest pending age* crossing
threshold — depth alone hides a stalled relay behind low traffic.

### III.3 Inbox & Idempotency — Consumer Side

**REL-020** — Every message consumer **MUST** be idempotent. Processing the same message twice
**MUST** produce exactly the same end state as processing it once, with no duplicated side
effects.

**REL-021** — Consumers **MUST** deduplicate using an **Inbox** table keyed by
`(message_id, consumer_name)`. The composite key is required because several consumers within
one service legitimately process the same message.

```sql
-- REL-021: canonical inbox schema
CREATE TABLE ordering.inbox_messages (
    message_id     UUID         NOT NULL,     -- envelope messageId from the producer
    consumer_name  VARCHAR(200) NOT NULL,     -- fully-qualified consumer type
    event_type     VARCHAR(200) NOT NULL,
    received_at    TIMESTAMPTZ  NOT NULL DEFAULT now(),
    processed_at   TIMESTAMPTZ  NULL,
    status         SMALLINT     NOT NULL,     -- 0=InProgress 1=Processed 2=Dead
    attempt_count  INT          NOT NULL DEFAULT 0,
    last_error     TEXT         NULL,
    PRIMARY KEY (message_id, consumer_name)
);
```

**REL-022** — The inbox insert and the business state change **MUST** commit in the **same local
transaction**. Marking a message processed outside the business transaction reintroduces the
dual-write problem on the consumer side.

```csharp
// ✅ REQUIRED — REL-022: dedup record and business effect are atomic
public async Task Consume(ConsumeContext<OrderPlaced> ctx)
{
    await using var tx = await _db.Database.BeginTransactionAsync(ctx.CancellationToken);

    // REL-021: claim the message; PK violation ⇒ already handled ⇒ ack and return
    if (!await _inbox.TryClaimAsync(ctx.MessageId!.Value, ConsumerName, ctx.CancellationToken))
    {
        _log.LogInformation("Duplicate {MessageId} ignored (REL-021)", ctx.MessageId);
        return;   // ack: the effect already exists
    }

    await _handler.HandleAsync(ctx.Message, ctx.CancellationToken);   // business effect
    await _inbox.MarkProcessedAsync(ctx.MessageId.Value, ConsumerName, ctx.CancellationToken);

    await _db.SaveChangesAsync(ctx.CancellationToken);
    await tx.CommitAsync(ctx.CancellationToken);
}
```

**REL-023** — Where the consumer's effect is **naturally idempotent**, the inbox **MAY** be
omitted, but the spec **MUST** state explicitly why. Naturally idempotent effects include: an
absolute `SET` to a value derived solely from the event, an upsert keyed by a natural key, and a
no-op-if-exists insert. Relative mutations (`balance = balance - 10`, `stock = stock - 1`) are
**never** naturally idempotent and **MUST** use the inbox.

**REL-024** — Technical deduplication (inbox) and **business idempotency** are distinct
obligations, and both **MUST** be satisfied. The inbox stops the same *message* twice; business
idempotency stops the same *intent* arriving as two different messages. A retried client request
with a fresh `messageId` sails through the inbox untouched — only a business-level natural key or
idempotency key (EDG-020) stops it.

**REL-025** — Inbox records **MUST** be retained at least as long as the broker's maximum
redelivery window plus the maximum retry backoff, and **SHOULD** be retained 7 days. Purging
earlier reopens the duplicate window.

**REL-026** — A consumer **MUST NOT** acknowledge a message before its effect is durably
committed. Acknowledge-then-process loses messages on crash and **MUST** fail code review.

**REL-027** — Consumers **MUST** be prepared for **out-of-order** delivery except where REL-006
guarantees ordering for that aggregate. Where ordering matters, the consumer **MUST** carry a
version or sequence check and **MUST** discard or park events older than the state it already
holds.

```csharp
// REL-027: stale-event guard for a denormalized read model
if (incoming.AggregateVersion <= existing.LastAppliedVersion)
{
    _log.LogDebug("Stale event v{In} <= v{Have}; discarded", 
        incoming.AggregateVersion, existing.LastAppliedVersion);
    return;
}
```

### III.4 Message Envelope

**REL-030** — Every message crossing the broker **MUST** carry this envelope. Field semantics are
governed in detail by `specs/event-governance.md`; the envelope itself is constitutional.

```jsonc
{
  "messageId":     "01JBQ8F5X0K3M9WZ2N7YQH4T6V",   // unique; the inbox dedup key (REL-021)
  "eventType":     "ordering.order.placed",         // COM-033
  "schemaVersion": 1,
  "occurredAt":    "2026-09-03T09:14:22.481Z",      // business time, UTC, ISO-8601
  "correlationId": "01JBQ8EXAMPLECORRELATION01",    // OBS-001: constant across the whole flow
  "causationId":   "01JBQ8F5X0K3M9WZ2N7YQH4T6V",    // OBS-003: the message that caused this one
  "traceparent":   "00-4bf92f...-00f067aa0ba902b7-01", // OBS-010: W3C Trace Context
  "aggregateType": "Order",
  "aggregateId":   "ORD-2026-0009184",              // REL-006 partition key
  "producer":      "ordering-service@2.3.1",
  "payload":       { }
}
```

**REL-031** — `messageId` **MUST** be generated by the producer at outbox-insert time and
**MUST NOT** be regenerated on retry. A relay that mints a new id per publish attempt destroys
consumer deduplication and **MUST** fail code review.

---

## Article IV — Transaction Boundaries & Consistency (`TXN`)

### IV.1 The Aggregate Is the Consistency Boundary

**TXN-001** — The Aggregate Root is the unit of consistency. All invariants inside one aggregate
**MUST** hold at every commit. Invariants spanning two aggregates **MUST NOT** be enforced
transactionally; they **MUST** be reached through eventual consistency (Saga, Article VI) or the
boundary **MUST** be redrawn.

**TXN-002** — One transaction **SHOULD** modify exactly one aggregate instance. Modifying several
aggregates in one transaction is permitted only when they belong to the same context *and* the
spec records why the boundary cannot be redrawn.

**TXN-003** — A transaction **MUST NOT** span two Bounded Contexts. There is no two-phase commit
in this platform. Distributed atomicity **MUST** be achieved with a Saga and compensating
actions (Article VI), never with XA/2PC.

### IV.2 Where Strong Consistency Is Mandatory

**TXN-010** — The following operations **MUST** execute inside a single local ACID transaction.
Eventual consistency is **NOT** acceptable for any of them:

| Operation | Atomic unit |
|---|---|
| Deducting reserved stock on order confirmation | stock row + reservation row + audit row |
| Recording a payment result | payment row + payment attempt row + outbox row |
| Applying a refund | refund row + payment balance + outbox row |
| Any ledger/balance mutation | balance + ledger entry |
| Any business write + its integration events | business rows + outbox rows (REL-002) |

**TXN-011** — Money **MUST** be represented as `decimal` with explicit scale, never `float` or
`double`, and **MUST** always carry an ISO-4217 currency code. A bare numeric amount without
currency **MUST NOT** cross any boundary.

**TXN-012** — Order totals, line prices, discounts, taxes, and shipping fees **MUST** be
recalculated server-side at command time. Amounts supplied by a client **MUST** be treated as
untrusted display values and **MUST NOT** be persisted or charged.

### IV.3 Eventual Consistency Contracts

**TXN-020** — Every eventually-consistent read model **MUST** declare, in its feature spec: its
source events, its expected convergence window (p50/p99), and the user-visible behaviour while
divergent.

**TXN-021** — A UI or API that reads an eventually-consistent projection immediately after a
write **MUST** be designed for the stale case. Acceptable techniques: return the authoritative
value from the command response, read-your-writes routing, or an explicit "processing" state.
Hiding the window behind a client-side `sleep` **MUST NOT** be used.

**TXN-022** — Records for orders, payments, refunds, and inventory movements **MUST NOT** be
hard-deleted. Use status transitions or soft deletion, backed by an append-only audit trail
recording actor, timestamp, and before/after values.

---

## Article V — Resilience: Retry, Circuit Breaker, DLQ (`RES`)

### V.1 Failure Classification

**RES-001** — Every remote failure **MUST** be classified before any retry decision is made.
Retrying a non-transient failure wastes capacity and, for money operations, causes real harm.

| Class | Examples | Retry? |
|---|---|---|
| **Transient** | Timeout, connection reset, 503, 429, broker unavailable, deadlock/serialization failure | **MUST** retry with backoff |
| **Permanent** | 400, 401, 403, 404, 422, schema validation failure, business rule rejection | **MUST NOT** retry |
| **Ambiguous** | Timeout on a *write* — outcome unknown | **MUST** retry only if the operation is idempotent (REL-020 / EDG-020) |

**RES-002** — An **ambiguous write** — a timeout where the request may or may not have been
applied — **MUST NOT** be retried unless protected by an idempotency key. Blind retry of an
ambiguous payment authorization is how customers get charged twice.

### V.2 Retry Policy

**RES-010** — All retries **MUST** use **exponential backoff with jitter**. Fixed-interval retry
**MUST NOT** be used: it synchronises every client into a thundering herd against a service that
is already struggling.

```
delay(n) = min(base × 2^(n-1), cap) × jitter
    base   = 1s   (RECOMMENDED default)
    cap    = 60s  (RECOMMENDED default)
    jitter = uniform random in [0.5, 1.5]   — full jitter is RECOMMENDED

n=1 → ~1s     n=2 → ~2s     n=3 → ~4s
n=4 → ~8s     n=5 → ~16s    n=6 → ~32s
```

**RES-011** — Every retry policy **MUST** declare a finite maximum attempt count *and* a maximum
total elapsed budget. Infinite retry **MUST NOT** be configured. **RECOMMENDED** defaults:

| Context | Max attempts | Total budget | On exhaustion |
|---|---|---|---|
| Synchronous request-scoped call (gRPC/HTTP) | 3 | 2s | Fail the request; surface a typed error |
| Message consumer, transient failure | 5 | 5m | Route to DLQ (RES-030) |
| Outbox relay publish | 10 | 1h | Mark `Dead` (REL-009) + alert |
| External payment gateway | 3 | 30s | Mark attempt `Unknown`; reconcile (EDG-024) |

**RES-012** — Retries **MUST** be applied at exactly one layer of any given call path. Nested
retry (HTTP client retries × Polly retries × consumer retries) multiplies into an accidental
denial-of-service against your own dependency: 3 × 3 × 5 = 45 attempts from one logical request.
The owning layer **MUST** be named in the spec.

**RES-013** — Message consumers **SHOULD** use two-tier retry: a short in-process immediate retry
(2–3 attempts, sub-second, for blips) followed by a broker-level delayed redelivery for longer
outages. In-process retry **MUST NOT** hold a database transaction or a broker delivery open
across the delay.

### V.3 Circuit Breaker

**RES-020** — Every call to a remote dependency — internal gRPC, external HTTP, payment gateway,
carrier API, SMS/email provider — **MUST** be wrapped in a circuit breaker. One failing
dependency **MUST NOT** be able to exhaust the caller's threads or connections and take the
platform down with it.

**RES-021** — Circuit breakers **MUST** implement the three-state model with these
**RECOMMENDED** defaults, tuned per dependency and recorded in the spec:

```
CLOSED ──(failure ratio ≥ 50% over ≥ 20 calls in 30s)──► OPEN
OPEN   ──(after break duration 30s)──────────────────────► HALF-OPEN
HALF-OPEN ──(1 trial call succeeds)──────────────────────► CLOSED
HALF-OPEN ──(1 trial call fails)─────────────────────────► OPEN (reset timer)
```

**RES-022** — A minimum-throughput threshold **MUST** be configured. A breaker that opens on
"1 of 1 calls failed" will flap continuously on a low-traffic endpoint.

**RES-023** — Behaviour while a breaker is **OPEN** **MUST** be specified per dependency — the
breaker itself is not the design, the fallback is:

| Dependency down | Required behaviour | Rule |
|---|---|---|
| Search | Fall back to category browse | RES-040 |
| Recommendations | Render page without the block | RES-040 |
| Pricing (internal) | Fail the request — **MUST NOT** guess a price | TXN-012 |
| Payment gateway | Reject new checkouts with a typed retryable error; **MUST NOT** mark orders paid | EDG-023 |
| Inventory | **MUST NOT** allow the order — fail closed | EDG-001 |
| Email/SMS | Queue for later; **MUST NOT** fail the business operation | COM-030 |

**RES-024** — Every synchronous outbound call **MUST** have an explicit timeout, and the sum of
downstream timeouts **MUST NOT** exceed the caller's own budget. A caller with a 1s SLA **MUST
NOT** invoke a dependency configured with a 5s timeout.

**RES-025** — Bulkheads **SHOULD** be applied: concurrency limits per dependency so saturation of
one cannot consume the whole connection or thread pool.

### V.4 Dead Letter Queue

**RES-030** — Every consumer queue **MUST** have a Dead Letter Queue configured. A message whose
retry budget is exhausted **MUST** be routed to the DLQ. It **MUST NOT** be silently dropped, and
it **MUST NOT** be acknowledged as successful.

**RES-031** — A DLQ'd message **MUST** retain: the original payload, the full envelope
(including `correlationId` and `traceparent`), the consumer name, the attempt count, the final
exception with stack trace, and the timestamp of first and last attempt. A DLQ entry that cannot
be diagnosed without reproducing the failure is inadequately captured.

**RES-032** — DLQ depth **MUST** be monitored and **MUST** alert on any non-zero value for a
money- or inventory-related queue. A silent DLQ is indistinguishable from data loss.

**RES-033** — A documented **replay procedure** **MUST** exist for every DLQ: how to inspect,
how to fix or skip, how to re-inject. Replay **MUST** pass through the normal inbox path
(REL-021) so a message already partially processed is not duplicated by the replay itself.

**RES-034** — A **poison message** — one that will never succeed, such as a malformed payload or
a reference to a permanently deleted entity — **MUST** be moved to the DLQ on first permanent
classification (RES-001) without consuming its transient retry budget.

**RES-035** — DLQ messages **MUST NOT** be deleted without a recorded decision. Skipping a
message is a data-loss event and **MUST** be logged with an owner and a reason.

### V.5 Graceful Degradation

**RES-040** — Every feature spec **MUST** state the degradation behaviour of each dependency it
introduces. Partial function **MUST** be preferred over total failure, except where correctness
forbids it (RES-023).

**RES-041** — Degradation **MUST** fail **closed** for money and inventory: when inventory
availability cannot be determined, the order **MUST** be rejected. Overselling is more expensive
than a lost sale.

**RES-042** — Target SLA for the checkout path is **99.9% uptime**. Any change to a checkout-path
dependency **MUST** state its effect on that budget.

---

## Article VI — Distributed Transactions: Saga & Compensation (`SAG`)

### VI.1 When a Saga Is Required

**SAG-001** — Any business transaction spanning two or more Bounded Contexts **MUST** be
implemented as a Saga: a sequence of local transactions, each with a defined compensating action.
Two-phase commit, XA, and distributed locks **MUST NOT** be used.

**SAG-002** — Every Saga **MUST** be specified before it is implemented. The spec **MUST**
enumerate, in a table: each step, its owning context, its command, its success event, its failure
event, and its compensating action. A Saga discovered by reading code is an incident waiting to
happen.

### VI.2 Orchestration vs Choreography

**SAG-010** — Sagas involving **money, inventory, or a customer-visible commitment** **MUST** use
**orchestration** — an explicit state machine owning the flow. Choreography **MUST NOT** be used
for these flows: no single component knows the state, which makes them undebuggable at 3am.

**SAG-011** — Choreography (each context reacting to events with no central coordinator) **MAY**
be used only for flows that are: fewer than 3 steps, without compensation requirements, and
without customer-visible commitment. Notification fan-out and analytics ingestion qualify.

**SAG-012** — Each Saga **MUST** have exactly one owning context, named in the spec. The
orchestrator for order placement **MUST** live in `Ordering`. An orchestrator **MUST NOT** be
placed in a context that does not own the business outcome.

**SAG-013** — The orchestrator **MUST NOT** reach into other contexts' databases. It coordinates
strictly by sending commands and consuming events over the broker.

### VI.3 Saga State

**SAG-020** — Saga state **MUST** be persisted durably after every transition, in the
orchestrator's own database. An in-memory saga **MUST NOT** be deployed — it loses every
in-flight business transaction on restart.

```sql
-- SAG-020: saga instance persistence
CREATE TABLE ordering.saga_order_placement (
    correlation_id   UUID         PRIMARY KEY,   -- == OBS-001 correlationId for the whole flow
    order_id         VARCHAR(100) NOT NULL UNIQUE,
    current_state    VARCHAR(60)  NOT NULL,      -- 'AwaitingStockReservation'
    row_version      INT          NOT NULL,      -- SAG-022 optimistic concurrency
    started_at       TIMESTAMPTZ  NOT NULL,
    updated_at       TIMESTAMPTZ  NOT NULL,
    deadline_at      TIMESTAMPTZ  NULL,          -- SAG-030
    completed_at     TIMESTAMPTZ  NULL,
    outcome          VARCHAR(30)  NULL,          -- Completed | Compensated | Failed
    compensation_log JSONB        NOT NULL DEFAULT '[]'::jsonb,  -- SAG-025
    payload          JSONB        NOT NULL
);
CREATE INDEX ix_saga_deadline ON ordering.saga_order_placement (deadline_at)
    WHERE completed_at IS NULL;
```

**SAG-021** — The Saga's `correlation_id` **MUST** be the same `correlationId` carried in every
message envelope for that flow (REL-030, OBS-001). One identifier, end to end.

**SAG-022** — Saga state transitions **MUST** be concurrency-safe. Two events arriving
simultaneously for the same saga instance **MUST NOT** interleave into a corrupt state; the
orchestrator **MUST** use optimistic concurrency on `row_version` or a row-level lock.

**SAG-023** — Every saga step transition **MUST** be idempotent (REL-020). Redelivery of a step's
success event **MUST NOT** advance the saga twice.

### VI.4 Compensating Actions

**SAG-025** — Every step that produces an externally visible effect **MUST** define a compensating
action **before** that step is implemented. A step without a defined compensation **MUST NOT**
be merged.

**SAG-026** — Compensation is **semantic**, not a rollback. It **MUST** be modelled as a new
business fact, not as an erasure of history. A captured payment is compensated by a **refund**
record, not by deleting the payment row (TXN-022).

**SAG-027** — Compensating actions **MUST** be idempotent and **MUST** be retryable indefinitely
within their budget. A compensation that fails permanently leaves the system in an inconsistent
state and **MUST** escalate to a human via alert plus a durable task, never be silently
abandoned.

**SAG-028** — Compensation **MUST** run in reverse order of the completed steps, and **MUST**
compensate only steps recorded as completed in `compensation_log`.

**SAG-029** — Some actions are **not compensable** — a dispatched shipment, a sent email, a
delivered SMS. Non-compensable steps **MUST** be ordered **last** in the Saga, after every
compensable step has succeeded. A Saga that ships goods before authorizing payment is
mis-ordered by construction.

### VI.5 Timeouts and Stuck Sagas

**SAG-030** — Every Saga **MUST** define an overall deadline and a per-step timeout. On expiry,
the Saga **MUST** transition to compensation — it **MUST NOT** wait indefinitely for an event
that may never arrive.

**SAG-031** — Sagas incomplete beyond their deadline **MUST** be detected by a monitor and
**MUST** alert. Stuck-saga count **MUST** be a dashboard metric (OBS-030).

**SAG-032** — A manual intervention path **MUST** exist for every money-bearing Saga: an
operator-facing capability to inspect state and force completion or compensation, fully audited.

### VI.6 Reference Saga — Order Placement

**SAG-040** — The order placement flow **MUST** follow this structure. Deviations **MUST** be
recorded as an ADR.

```
Ordering (orchestrator)
  1. Order created, status = Pending                      [local tx + outbox]
  2. → inventory.reserve-stock            ┐
       ✓ stock.reserved                   │ compensate: inventory.release-reservation
       ✗ stock.reservation-rejected       ┘
  3. → payment.authorize                  ┐
       ✓ payment.authorized               │ compensate: payment.void-authorization
       ✗ payment.declined                 ┘
  4. → inventory.commit-reservation       ┐  (stock physically deducted)
       ✓ stock.committed                  │ compensate: inventory.restock
  5. → payment.capture                    ┐
       ✓ payment.captured                 │ compensate: payment.refund
       ✗ payment.capture-failed           ┘
  6. Order status = Confirmed                             [local tx + outbox]
  7. → shipping.create-shipment              ⚠ NON-COMPENSABLE — MUST be last (SAG-029)
  8. → notification.send-confirmation        ⚠ NON-COMPENSABLE — fire-and-forget
```

| Step | Failure at this step compensates | Customer-visible outcome |
|---|---|---|
| 2 | — | Order rejected: out of stock |
| 3 | release reservation | Order rejected: payment declined |
| 4 | void authorization, release reservation | Order rejected: stock unavailable |
| 5 | restock, void authorization | Order rejected: payment failed |
| 6+ | refund, restock | Order cancelled, refund issued |

**SAG-041** — Stock **MUST** be *reserved* (step 2) before payment is authorized, and *committed*
only after authorization succeeds. Authorizing payment before securing stock produces charged
customers with unfulfillable orders and **MUST NOT** be implemented.

---

## Article VII — Business Edge Cases (`EDG`)

> This article encodes the failures that actually cost money in e-commerce. Each rule exists
> because the obvious implementation is wrong under concurrency.

### VII.1 Overselling Prevention

**EDG-001** — Stock **MUST NOT** be oversold. Where availability cannot be determined with
certainty, the system **MUST** fail closed and reject the order (RES-041).

**EDG-002** — The read-then-write pattern **MUST NOT** be used for stock. It is a lost-update
race, and under concurrency it *will* oversell:

```csharp
// ❌ FORBIDDEN — violates EDG-002. Two concurrent requests both read 1 and both write 0.
var item = await _db.Stock.FirstAsync(s => s.Sku == sku, ct);
if (item.Available >= qty)          // ← T1 and T2 both pass here
{
    item.Available -= qty;          // ← both write; one decrement is lost
    await _db.SaveChangesAsync(ct);
}
```

**EDG-003** — Stock mutation **MUST** use one of the three approved strategies below. The choice
**MUST** be recorded in the feature spec.

**Strategy A — Atomic conditional update (REQUIRED default).**
The database enforces the invariant in one statement. Correct, simple, and lock-free.

```sql
-- EDG-003(A): the WHERE clause is the invariant. 0 rows affected ⇒ insufficient stock.
UPDATE inventory.stock_items
   SET available   = available - @qty,
       reserved    = reserved  + @qty,
       row_version = row_version + 1,
       updated_at  = now()
 WHERE sku       = @sku
   AND available >= @qty;          -- ← atomic guard; no read-then-write window
```

```csharp
var affected = await _db.Database.ExecuteSqlInterpolatedAsync(/* … */, ct);
if (affected == 0)
    throw new InsufficientStockException(sku, qty);   // EDG-001: fail closed
```

**Strategy B — Optimistic concurrency (`MAY`, for aggregate-level rules).**
Use where the decision requires domain logic beyond a single column comparison. The write
**MUST** be guarded by `row_version`, and a `DbUpdateConcurrencyException` **MUST** be retried a
bounded number of times (RES-011) — never swallowed.

**Strategy C — Pessimistic row lock (`MAY`, high-contention only).**
`SELECT … FOR UPDATE` on the stock row. Permitted only for flash-sale-grade contention, and the
lock **MUST NOT** be held across any network call. Holding a row lock while calling a payment
gateway **MUST** fail code review.

**EDG-004** — Stock **MUST** be modelled as a **reservation**, not a direct decrement at checkout
start. Reservations **MUST** carry an expiry:

```
available  = on_hand − reserved     (what a new customer may take)
reserved   = held by in-flight checkouts, each with expires_at
on_hand    = physical stock
```

**EDG-005** — Every reservation **MUST** have a TTL (**RECOMMENDED** 15 minutes for standard
checkout, 5 minutes for flash sales) and expired reservations **MUST** be released automatically
by a sweeper. A reservation released only on explicit cancellation leaks stock permanently when
the customer closes the tab.

**EDG-006** — The reservation sweeper **MUST** be idempotent and **MUST** be safe to run
concurrently on multiple instances (`FOR UPDATE SKIP LOCKED`, per REL-005).

**EDG-007** — Reservation release **MUST** be idempotent. Releasing an already-released
reservation **MUST** be a no-op, never a second increment of `available`. This is the compensation
path in SAG-040 step 2 and *will* be retried.

**EDG-008** — Every stock movement **MUST** be recorded in an append-only ledger (`reserved`,
`released`, `committed`, `restocked`, `adjusted`), each row carrying the `correlationId` that
caused it. Without the ledger, a stock discrepancy is unauditable.

**EDG-009** — For flash sales and other extreme-contention events, a Redis-based counter **MAY**
front the database to shed load, provided: the decrement is atomic (Lua or `DECRBY` with a
guard), Redis is treated as an **admission control filter** and **NOT** as the source of truth,
and the database re-validates with Strategy A before any commitment. Redis **MUST NOT** be the
system of record for stock.

```lua
-- EDG-009: atomic admission filter. Rejects fast; the DB still enforces the real invariant.
local available = tonumber(redis.call('GET', KEYS[1]) or '0')
local requested = tonumber(ARGV[1])
if available < requested then return -1 end
return redis.call('DECRBY', KEYS[1], requested)
```

### VII.2 Payment Idempotency

**EDG-020** — Every write endpoint that moves money or creates an order **MUST** accept an
`Idempotency-Key` header and **MUST** enforce it. This covers: create order, authorize, capture,
refund, void.

**EDG-021** — Idempotency **MUST** be implemented as store-request-and-replay-response, not as a
mere duplicate check:

```
1. Client sends Idempotency-Key: <uuid>  (client-generated, stable across its own retries)
2. Server atomically INSERTs (key, endpoint, request_fingerprint) with status = InProgress
     ├─ insert succeeds  → first occurrence; process the request
     ├─ PK conflict, status = Completed  → return the STORED response verbatim (same status
     │                                     code, same body). MUST NOT re-execute.
     └─ PK conflict, status = InProgress → return 409 Conflict "request in progress".
                                           MUST NOT process concurrently.
3. On completion, store status code + response body against the key, status = Completed.
```

```sql
-- EDG-021: idempotency record
CREATE TABLE payment.idempotency_keys (
    key                  VARCHAR(255) NOT NULL,
    endpoint             VARCHAR(200) NOT NULL,
    request_fingerprint  CHAR(64)     NOT NULL,   -- SHA-256 of the canonical request body
    status               SMALLINT     NOT NULL,   -- 0=InProgress 1=Completed
    response_status_code INT          NULL,
    response_body        JSONB        NULL,
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT now(),
    expires_at           TIMESTAMPTZ  NOT NULL,
    PRIMARY KEY (key, endpoint)
);
```

**EDG-022** — The idempotency key **MUST** be bound to a fingerprint of the request body. If the
same key arrives with a *different* body, the server **MUST** reject with `422 Unprocessable
Entity` and **MUST NOT** process it. Silently returning the first response to a different request
is a correctness bug that hides client defects.

**EDG-023** — The idempotency record and the business effect **MUST** be committed in the same
transaction (identical reasoning to REL-022). Writing the key outside the transaction reopens the
double-charge window on crash.

**EDG-024** — Calls to external payment gateways **MUST** pass a gateway-level idempotency key
derived deterministically from the platform's own identifiers (for example
`{orderId}:{attemptNumber}`), so that a retry after an ambiguous timeout (RES-002) reaches the
*same* gateway operation rather than creating a second charge.

**EDG-025** — Every payment attempt **MUST** be persisted with a tri-state outcome —
`Succeeded`, `Failed`, or **`Unknown`**. `Unknown` **MUST** be a first-class state, reached on
timeout, and **MUST** be resolved by an automated reconciliation job querying the gateway. An
implementation that treats a timeout as failure will double-charge; one that treats it as success
will ship unpaid goods.

**EDG-026** — A daily **reconciliation** job **MUST** compare the platform's payment records
against the gateway's settlement report. Discrepancies **MUST** alert and **MUST NOT**
auto-resolve.

**EDG-027** — Idempotency keys **MUST** be retained at least 24 hours (**RECOMMENDED** 7 days for
payment endpoints) and **MUST** be purged thereafter. Retention shorter than the client's maximum
retry window reopens the duplicate window.

**EDG-028** — Refunds **MUST** be idempotent and **MUST** be guarded against exceeding the
captured amount. Cumulative refunds against a payment **MUST** be validated within the same
transaction that records the refund.

### VII.3 Other Recurring Edge Cases

**EDG-030** — Cart pricing **MUST** be re-validated at checkout. A price or promotion change
between add-to-cart and checkout **MUST** be surfaced to the customer, never silently applied.

**EDG-031** — Promotion and voucher redemption **MUST** be atomic and **MUST** enforce
per-customer and global usage limits under concurrency, using EDG-003 Strategy A. Vouchers are
a lost-update race identical to stock.

**EDG-032** — Order state transitions **MUST** be validated against an explicit state machine.
An illegal transition (for example `Cancelled → Shipped`) **MUST** be rejected by the aggregate,
not merely prevented by the UI.

**EDG-033** — Duplicate order submission (double-click, mobile retry) **MUST** be prevented by
EDG-020, not by client-side button disabling.

---

## Article VIII — Observability (`OBS`)

### VIII.1 Correlation

**OBS-001** — Every request entering the platform **MUST** be assigned a `Correlation-ID` at the
edge if one is not supplied. It **MUST** remain constant for the entire business flow, across
every synchronous hop, every message, and every Saga step.

**OBS-002** — The `Correlation-ID` **MUST** propagate through: HTTP headers (`X-Correlation-ID`),
gRPC metadata (`x-correlation-id`), message envelopes (REL-030 `correlationId`), and the outbox
`headers` column. A context that drops it **MUST** fail code review — the flow becomes untraceable
from that point onward.

**OBS-003** — Every message **MUST** carry a `causationId`: the `messageId` of the message that
directly caused it. Correlation answers *"which business flow?"*; causation answers *"what
triggered this exact message?"* Together they reconstruct the full causal tree of a failure.

**OBS-004** — Propagation **MUST** be implemented once, in shared middleware/filters in
`src/BuildingBlocks/`, and **MUST NOT** be re-implemented per service. Manual per-handler
propagation is forgotten exactly where it matters most.

### VIII.2 Distributed Tracing

**OBS-010** — Distributed tracing **MUST** use OpenTelemetry with **W3C Trace Context**
(`traceparent` / `tracestate`). Proprietary trace headers **MUST NOT** be introduced.

**OBS-011** — Trace context **MUST** cross the asynchronous boundary. The producer **MUST**
inject `traceparent` into the outbox `headers` at enqueue time, and the consumer **MUST** extract
it and continue the trace as a linked span. A trace that stops at the broker is the single most
common gap in event-driven observability and is **MUST NOT**.

**OBS-012** — Spans **MUST** be created for: inbound HTTP/gRPC handling, outbound remote calls,
database commands, message publish, message consume, and each Saga step transition.

**OBS-013** — Span attributes **MUST** include `correlation_id`, `aggregate_id` where applicable,
and the business operation name. Span attributes **MUST NOT** contain PII, card data, tokens, or
credentials (SEC-012).

### VIII.3 Structured Logging

**OBS-020** — All logs **MUST** be structured JSON emitted to stdout, and **MUST** be shipped to
a centralized log store. Free-text-only logs and file-based logging on the container **MUST NOT**
be used.

**OBS-021** — Every log entry **MUST** carry these fields:

```jsonc
{
  "timestamp":     "2026-09-03T09:14:22.481Z",
  "level":         "Error",
  "service":       "ordering-service",
  "version":       "2.3.1",
  "environment":   "production",
  "correlationId": "01JBQ8EXAMPLECORRELATION01",   // OBS-001
  "traceId":       "4bf92f3577b34da6a3ce929d0e0e4736",
  "spanId":        "00f067aa0ba902b7",
  "message":       "Stock reservation rejected",
  "eventType":     "inventory.stock.reservation-rejected.v1",
  "aggregateId":   "ORD-2026-0009184",
  "error": { "type": "InsufficientStockException", "message": "…", "stackTrace": "…" }
}
```

**OBS-022** — Logs **MUST NOT** contain: full card numbers or CVV (ever, in any form), passwords,
tokens, API keys, full PII. Customer email and phone **MUST** be masked
(`j***@example.com`, `+8490****123`).

**OBS-023** — Log levels **MUST** be used consistently: `Error` = requires human action;
`Warning` = degraded but handled (breaker opened, retry exhausted, compensation triggered);
`Information` = business milestones (order placed, payment captured); `Debug` = development only,
**MUST NOT** be enabled by default in production.

**OBS-024** — An expected business rejection (out of stock, payment declined) **MUST NOT** be
logged at `Error`. Alert fatigue caused by logging normal outcomes as errors is how real errors
get ignored.

### VIII.4 Metrics, Alerting, Health

**OBS-030** — Every service **MUST** publish, at minimum:

| Category | Required metrics |
|---|---|
| RED (per endpoint/consumer) | request rate, error rate, duration histogram (p50/p95/p99) |
| Messaging | outbox pending depth, **oldest pending age**, publish failure rate, consumer lag, DLQ depth |
| Saga | active count, completion rate, compensation rate, **stuck count** |
| Business | checkout error rate, payment success rate, **oversell count (MUST be 0)**, cart abandonment |
| Resilience | circuit breaker state per dependency, retry count, timeout count |

**OBS-031** — Alerts **MUST** be configured for at least: payment error rate above threshold,
DLQ depth > 0 on money/inventory queues, oldest outbox pending age above threshold, stuck sagas,
circuit breaker OPEN beyond a grace period, oversell count > 0, and checkout latency SLA breach.

**OBS-032** — Every alert **MUST** link to a runbook. An alert without a documented response is
noise and **SHOULD** be deleted or fixed.

**OBS-033** — Every service **MUST** expose `/health/live` (process alive) and `/health/ready`
(dependencies reachable) for the orchestrator. Readiness **MUST** check the database and broker;
it **MUST NOT** check optional dependencies, or a degraded non-critical dependency will remove
healthy instances from rotation.

**OBS-034** — It **MUST** be possible to reconstruct a complete business flow — order placed →
stock reserved → payment authorized → stock committed → payment captured → shipment created →
email sent — from a single `Correlation-ID`, within minutes, without reading application code.
This is the acceptance test for this entire article.

---

## Article IX — Security & Compliance (`SEC`)

> Carried forward from constitution v1.0.1 Principle III. **NON-NEGOTIABLE**.

### IX.1 Payment Data

**SEC-001** — Card data (PAN, CVV, expiry, magnetic-stripe data) **MUST NOT** be stored,
logged, cached, or transited through platform-owned systems in any form. Payments **MUST** be
processed by a PCI-DSS compliant gateway using tokenization or a hosted field/redirect flow.

**SEC-002** — The platform **MAY** store only the gateway token, the last four digits, the brand,
and the expiry month/year for display purposes.

### IX.2 Data Protection

**SEC-010** — All PII (name, phone, address, email, date of birth) **MUST** be encrypted at rest
and in transit. TLS 1.2+ is **REQUIRED** on every network hop, including internal gRPC.

**SEC-011** — Secrets (connection strings, API keys, signing keys) **MUST** be supplied by a
secret manager or injected configuration. Secrets **MUST NOT** appear in source control,
container images, or log output.

**SEC-012** — PII and secrets **MUST NOT** appear in logs (OBS-022), trace attributes (OBS-013),
error messages returned to clients, or event payloads that cross a context boundary without a
documented need.

**SEC-013** — The platform **MUST** comply with the data-protection law of each market it serves
(Decree 13/2023 in Vietnam; GDPR where EU customers are served), including data subject access
and erasure. Erasure **MUST** be reconciled with TXN-022 by anonymisation of retained financial
records rather than their deletion.

### IX.3 Authentication & Authorization

**SEC-020** — Authentication **MUST** use OAuth2/OIDC or JWT with refresh tokens, with rate
limiting and lockout on the credential endpoints (state held per ARC-041 in Redis).

**SEC-021** — Authorization **MUST** be enforced server-side on every endpoint and every message
consumer, by role and by resource ownership. Hiding a UI control is **NOT** an authorization
control.

**SEC-022** — Every request **MUST** be authorized against the *resource*, not only the role. A
customer with role `customer` **MUST NOT** be able to read another customer's order by changing
the identifier in the URL.

**SEC-023** — Passwords **MUST** be hashed with a memory-hard algorithm (Argon2id **RECOMMENDED**,
bcrypt cost ≥ 12 acceptable). Fast hashes (MD5, SHA-*) **MUST NOT** be used.

### IX.4 Application Security

**SEC-030** — OWASP Top 10 is the floor. Parameterized queries or an ORM **MUST** be used; string
concatenation into SQL **MUST NOT** appear in the codebase.

**SEC-031** — All input **MUST** be validated server-side against an explicit schema at the
`Application` boundary. Client-side validation is a UX affordance with no security value.

**SEC-032** — Dependencies **MUST** be scanned for known vulnerabilities in CI. A build with a
known critical vulnerability in a production dependency **MUST NOT** be deployed.

---

## Article X — Quality Gates (`QAG`)

> Carried forward from constitution v1.0.1 Principle V. **NON-NEGOTIABLE** for money and
> inventory paths.

**QAG-001** — TDD is **REQUIRED** for core business logic: pricing, cart, checkout, payment,
inventory, and every Saga. Tests are written first, **MUST** be observed failing, and only then
is the implementation written.

**QAG-002** — Minimum line coverage: **90%** for money and inventory modules, **80%** elsewhere.
Coverage below threshold **MUST** fail the pipeline. It is a gate, not a warning.

**QAG-003** — Domain logic **MUST** be tested without infrastructure. A domain test that requires
a database or a broker indicates an ARC-004 violation.

**QAG-004** — Every Saga **MUST** have tests covering the happy path *and* every compensation
path defined in SAG-025. An untested compensation path is an untested path that only ever runs
during an incident.

**QAG-005** — Idempotency **MUST** be tested explicitly: every consumer test suite **MUST**
include a case that delivers the same message twice and asserts a single effect.

**QAG-006** — Concurrency **MUST** be tested for stock decrement, voucher redemption, and payment
capture: N parallel attempts against a stock of M **MUST** yield exactly M successes and N−M
rejections.

**QAG-007** — Contract tests **MUST** exist between producers and consumers of every published
contract, so a breaking change fails in CI rather than in production (COM-042).

**QAG-008** — Integration tests for persistence, outbox, inbox, and consumers **MUST** run
against real infrastructure (Testcontainers), not in-memory substitutes. An in-memory provider
does not implement `SKIP LOCKED`, transaction isolation, or unique-constraint timing — the exact
behaviours these rules depend on.

**QAG-009** — Load testing **MUST** precede every major sales event, and **MUST** establish the
measured load ceiling and a scaling plan before the event.

**QAG-010** — Every PR containing business logic **MUST** ship with corresponding tests. Business
logic without tests **MUST NOT** be merged.

**QAG-011** — CI **MUST** pass before merge to the main branch. Deploying to production while
bypassing staging **MUST NOT** occur. Changes to checkout or payment **MUST** deploy via
blue-green or canary, and risky features **MUST** sit behind a feature flag.

---

## Article XI — Extension & Custom Rules (`EXT`)

> This article is the designated extension point. Add project-specific rules here without
> touching Articles I–X, so upstream amendments and your customisations never collide.

### XI.1 How To Add a Rule

**EXT-001** — A new custom rule **MUST** be added as a subsection of § XI.3 using the template in
§ XI.2, **MUST** be allocated an identifier from a reserved `900`–`999` range, and **MUST** be
registered in Appendix A.

**EXT-002** — Custom rules **MUST NOT** contradict Articles I–X. A custom rule that needs to
relax a core rule **MUST** instead go through the § Governance amendment process, or be raised as
a time-boxed waiver (§ XI.4).

**EXT-003** — Every custom rule **MUST** specify an **enforcement mechanism**. A rule nobody can
check is documentation, not governance. Acceptable mechanisms: an automated test, a CI check, a
lint rule, a schema validation, or a named reviewer checklist item.

**EXT-004** — Adding a custom rule is a **MINOR** version bump. Removing or materially
redefining one is **MAJOR** (§ Governance).

**EXT-005** — Reserved identifier ranges for custom rules, by article:

| Range | Article | Reserved for |
|---|---|---|
| `ARC-900`–`999` | I | Custom architecture/layering rules |
| `COM-900`–`999` | II | Custom communication rules |
| `REL-900`–`999` | III | Custom reliability rules |
| `TXN-900`–`999` | IV | Custom transaction/consistency rules |
| `RES-900`–`999` | V | Custom resilience rules |
| `SAG-900`–`999` | VI | Custom saga rules |
| `EDG-900`–`999` | VII | Custom business edge-case rules |
| `OBS-900`–`999` | VIII | Custom observability rules |
| `SEC-900`–`999` | IX | Custom security rules |
| `QAG-900`–`999` | X | Custom quality gates |
| `EXT-100`–`899` | XI | Rules belonging to no existing article |

### XI.2 Custom Rule Template

Copy this block verbatim when adding a rule.

````markdown
#### <RULE-ID> — <Short imperative title>

**Status**: Active | Proposed | Withdrawn in vX.Y.Z
**Added**: vX.Y.Z (YYYY-MM-DD)
**Owner**: <team or person accountable for this rule>
**Applies to**: <all services | named contexts | named layer>

**Rule**: <One sentence using MUST / SHOULD / MAY. Exactly one obligation per rule —
if it needs an "and", it is two rules.>

**Rationale**: <Why this exists. What went wrong, or what will go wrong without it.
A rule whose rationale cannot be written down is a preference, not a rule.>

**Enforcement**: <Automated test | CI check | lint rule | schema validation | review checklist>
`<concrete command, test name, or file path that performs the check>`

**Exceptions**: <None | the precise, enumerated conditions under which it does not apply>

**Example**:
```<language>
// ❌ Violates <RULE-ID>
<counter-example>

// ✅ Complies with <RULE-ID>
<correct example>
```
````

### XI.3 Registered Custom Rules

*No custom rules registered yet. Add them below using the template in § XI.2, keeping subsections
ordered by identifier.*

<!--
EXAMPLE (delete when adding your first real rule):

#### EDG-900 — Gift card balances MUST be checked atomically

**Status**: Active
**Added**: v2.1.0 (2026-10-01)
**Owner**: Payments team
**Applies to**: Payment context

**Rule**: Gift card redemption MUST use the atomic conditional update pattern (EDG-003
Strategy A). Read-then-write against a gift card balance MUST NOT be used.

**Rationale**: A gift card balance is a lost-update race identical to stock (EDG-002).
Concurrent redemption of the same card across two tabs drains it twice.

**Enforcement**: Automated test
`tests/Services/Payment/…/GiftCardConcurrencyTests.Redeem_is_atomic_under_parallel_load`

**Exceptions**: None.
-->

### XI.4 Waivers

**EXT-010** — A **MUST** rule **MAY** be waived temporarily only through a recorded waiver. An
undocumented deviation is a violation, not a waiver.

**EXT-011** — Every waiver **MUST** record: the rule ID, the scope (which service/module/PR), the
justification, a named owner, the remediation plan, and a **hard expiry date**. A waiver without
an expiry date **MUST NOT** be granted.

**EXT-012** — Waivers **MUST** be tracked in § XI.5 and **MUST** be reviewed at expiry. An expired
waiver that has not been renewed makes the code non-compliant and **MUST** block the next release
touching that module.

**EXT-013** — Rules in Articles III (REL), VII (EDG), and IX (SEC) **MUST NOT** be waived. They
guard data loss, financial correctness, and legal compliance respectively; there is no schedule
pressure that justifies a double charge.

### XI.5 Active Waivers

| Waiver ID | Rule | Scope | Justification | Owner | Expires | Status |
|---|---|---|---|---|---|---|
| *(none)* | — | — | — | — | — | — |

---

## Governance

### G.1 Authority

This constitution supersedes every other project convention and document. Where any artifact
conflicts with it, this document wins.

### G.2 Amendment Procedure

1. An amendment **MUST** be proposed as a pull request editing this file, stating the rule IDs
   affected and the reasoning.
2. An amendment touching a **NON-NEGOTIABLE** article (III, VII, IX, X) **MUST** include a
   migration plan with a concrete deadline for existing non-compliant code.
3. An amendment **MUST** update the Sync Impact Report at the top of this file and Appendix A.
4. An amendment takes effect when merged; the merge date becomes `Last Amended`.

### G.3 Versioning Policy

Semantic versioning applied to this document:

| Bump | Trigger |
|---|---|
| **MAJOR** | A rule is removed or redefined in a backward-incompatible way; article restructuring |
| **MINOR** | A new rule or article is added; existing guidance materially expanded; a custom rule registered |
| **PATCH** | Wording clarification, typo, formatting, example fix — no change in obligation |

### G.4 Compliance Review

- Every PR review **MUST** verify the change against this constitution. A reviewer **MAY** block
  a merge citing a rule ID alone.
- Every `/speckit-plan` **MUST** include a Constitution Check before design work begins, and
  **MUST** cite the rule IDs it is designing against.
- Every `/speckit-specify` output **MUST** be checkable against Appendix B.
- Complexity beyond what a rule requires **MUST** be justified in writing in the plan; what
  cannot be justified **MUST** be simplified.
- Runtime agent guidance (`CLAUDE.md` or equivalent) **MUST NOT** contradict this constitution.

### G.5 Relationship to Spec Kit

`.specify/memory/constitution.md` is the path the `/speckit-*` tooling reads at runtime. That file
**MUST** either be this document or an unambiguous pointer to it. Two divergent constitutions
**MUST NOT** exist in this repository.

---

## Appendix A — Rule Index

| Article | Prefix | Range in use | Count | Negotiable? |
|---|---|---|---|---|
| I — Architectural Principles | `ARC` | 001–043 | 25 | Yes, via amendment |
| II — Communication Standards | `COM` | 001–042 | 25 | Yes, via amendment |
| III — Data Reliability | `REL` | 001–031 | 21 | **NON-NEGOTIABLE** |
| IV — Transaction Boundaries | `TXN` | 001–022 | 9 | Partially (TXN-010/011 non-negotiable) |
| V — Resilience | `RES` | 001–042 | 21 | Yes, via amendment |
| VI — Saga | `SAG` | 001–041 | 20 | Yes, via amendment |
| VII — Business Edge Cases | `EDG` | 001–033 | 22 | **NON-NEGOTIABLE** |
| VIII — Observability | `OBS` | 001–034 | 18 | Yes, via amendment |
| IX — Security & Compliance | `SEC` | 001–032 | 13 | **NON-NEGOTIABLE** |
| X — Quality Gates | `QAG` | 001–011 | 11 | **NON-NEGOTIABLE** |
| XI — Extension & Custom Rules | `EXT` | 001–013 | 9 | Meta |
| **Total** | | | **194** | |

## Appendix B — Pull Request Conformance Checklist

A reviewer **SHOULD** work this list. Any unchecked box **MUST** be either fixed or waived
(EXT-010) before merge.

**Architecture**
- [ ] Dependency rule respected; architecture tests pass (ARC-002, ARC-007)
- [ ] No cross-context type or table access (ARC-010, ARC-020)
- [ ] No state held in application memory (ARC-040)

**Communication**
- [ ] Communication style matches the COM-001 decision table
- [ ] gRPC calls set explicit deadlines (COM-022)
- [ ] No cross-context synchronous write (COM-024, COM-030)
- [ ] Contract changes versioned and compatibility-checked (COM-042)

**Reliability**
- [ ] State change and event emission committed in one transaction (REL-002)
- [ ] No direct publish after `SaveChangesAsync` (REL-001)
- [ ] Consumers idempotent; inbox used or naturally-idempotent justified (REL-020, REL-023)
- [ ] Inbox write inside the business transaction (REL-022)
- [ ] Envelope complete, `messageId` stable across retries (REL-030, REL-031)

**Resilience**
- [ ] Failures classified before retry (RES-001); ambiguous writes guarded (RES-002)
- [ ] Exponential backoff with jitter, finite budget (RES-010, RES-011)
- [ ] Retry at exactly one layer (RES-012)
- [ ] Circuit breaker + explicit timeout on every remote call (RES-020, RES-024)
- [ ] DLQ configured; degradation behaviour specified (RES-030, RES-040)

**Transactions & Sagas**
- [ ] One aggregate per transaction; no cross-context transaction (TXN-002, TXN-003)
- [ ] Saga specified with compensation table before implementation (SAG-002, SAG-025)
- [ ] Saga state persisted; transitions idempotent and concurrency-safe (SAG-020, SAG-022)
- [ ] Non-compensable steps ordered last (SAG-029)
- [ ] Deadline defined (SAG-030)

**Business Edge Cases**
- [ ] No read-then-write on stock, vouchers, or balances (EDG-002, EDG-031)
- [ ] Stock strategy chosen and recorded (EDG-003)
- [ ] Reservations carry TTL; release is idempotent (EDG-005, EDG-007)
- [ ] `Idempotency-Key` enforced with response replay (EDG-020, EDG-021)
- [ ] Gateway idempotency key deterministic; `Unknown` state handled (EDG-024, EDG-025)
- [ ] Amounts recalculated server-side; money is `decimal` + currency (TXN-011, TXN-012)

**Observability**
- [ ] `Correlation-ID` propagated across every hop including the broker (OBS-002, OBS-011)
- [ ] `causationId` set on emitted messages (OBS-003)
- [ ] Structured logs with required fields; no PII or secrets (OBS-021, OBS-022)
- [ ] Metrics and alerts added for new failure modes (OBS-030, OBS-031)

**Security & Quality**
- [ ] No card data stored; no secrets committed (SEC-001, SEC-011)
- [ ] Server-side authorization on resource, not just role (SEC-021, SEC-022)
- [ ] Tests written first; coverage gate met (QAG-001, QAG-002)
- [ ] Idempotency and concurrency tests present (QAG-005, QAG-006)
- [ ] Saga compensation paths tested (QAG-004)

---

*End of constitution. Related documents: `specs/contexts.md` (bounded context map),
`specs/event-governance.md` (event schema & versioning), `specs/guidelines.md` (code structure &
developer workflow), `specs/templates/feature-spec-template.md` (feature spec template).*
