# Event Governance — Schema, Format & Versioning

**Version**: 1.0.0 | **Last Amended**: 2026-09-03
**Governed by**: [`specs/constitution.md`](./constitution.md) v2.0.0
**Normative for**: every message published to the broker by any context

> Integration events are **published contracts**, not internal data structures. Once an event is
> in production, some consumer you have never met depends on its shape. This document defines how
> to write one, how to change one without breaking that consumer, and how to retire one.
>
> Constitutional basis: **COM-032** (domain vs integration events), **COM-033** (naming),
> **COM-034** (past-tense facts), **COM-036** (self-contained payloads), **COM-040/041/042**
> (contracts directory, review, automated compatibility checks), **REL-030** (envelope).

---

## 1. Wire Format — CloudEvents 1.0

**EVG-001** — Every message published to the broker **MUST** conform to **CloudEvents v1.0**
in **structured JSON mode**. Bespoke envelope formats **MUST NOT** be introduced.

### 1.1 Relationship to the Constitution's `REL-030` Envelope

`REL-030` defines the **logical** envelope — which pieces of information every message carries.
This document defines the **wire encoding** of that envelope. They describe the same thing at
different levels.

The mapping is exact and normative. Note that CloudEvents restricts extension attribute names to
**lowercase alphanumeric** — so the logical `correlationId` is written on the wire as
`correlationid`.

| `REL-030` logical field | CloudEvents attribute | Kind | Notes |
|---|---|---|---|
| `messageId` | `id` | core | Unique per message; **the inbox dedup key** (REL-021) |
| `eventType` | `type` | core | Includes the major version: `ordering.order.placed.v1` |
| `producer` | `source` | core | URI-reference: `/ordering-service` |
| `occurredAt` | `time` | core | RFC 3339 UTC, business time |
| `aggregateId` | `subject` | core | Also the broker partition/routing key (REL-006) |
| `schemaVersion` | `dataschema` | core | URI of the JSON Schema that validates `data` |
| `payload` | `data` | core | The business payload |
| `correlationId` | `correlationid` | **extension** | Constant across the whole flow (OBS-001) |
| `causationId` | `causationid` | **extension** | `id` of the message that caused this one (OBS-003) |
| `aggregateType` | `aggregatetype` | **extension** | `Order`, `Payment`, … |
| `traceparent` | `traceparent` | **extension** | W3C Trace Context (OBS-010, OBS-011) |
| `traceparent` (state) | `tracestate` | **extension** | Optional companion |

**EVG-002** — All eleven attributes above **MUST** be present on every published event.
`tracestate` is the sole **OPTIONAL** member.

### 1.2 Canonical Message

```jsonc
{
  "specversion":   "1.0",
  "id":            "01JBQ8F5X0K3M9WZ2N7YQH4T6V",
  "type":          "ordering.order.placed.v1",
  "source":        "/ordering-service",
  "subject":       "ORD-2026-0009184",
  "time":          "2026-09-03T09:14:22.481Z",
  "datacontenttype": "application/json",
  "dataschema":    "https://contracts.example.com/events/ordering/order.placed.v1.json",

  // ── extensions (lowercase alphanumeric — CloudEvents constraint) ──
  "correlationid": "01JBQ8EXAMPLECORRELATION01",
  "causationid":   "01JBQ8F5X0K3M9WZ2N7YQH4T6V",
  "aggregatetype": "Order",
  "traceparent":   "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",

  "data": {
    "orderId":    "ORD-2026-0009184",
    "customerId": "CUS-000481923",
    "placedAt":   "2026-09-03T09:14:22.481Z",
    "currency":   "VND",
    "lines": [
      { "sku": "SKU-1234", "name": "Wireless Mouse", "quantity": 2, "unitPrice": "250000.00" }
    ],
    "totals": {
      "subtotal": "500000.00", "shipping": "30000.00",
      "discount": "0.00",      "tax": "50000.00", "grandTotal": "580000.00"
    },
    "shippingAddress": {
      "line1": "12 Nguyen Trai", "ward": "Ben Thanh",
      "district": "District 1", "city": "Ho Chi Minh City", "countryCode": "VN"
    }
  }
}
```

**EVG-003** — Monetary amounts **MUST** be serialized as **strings**, not JSON numbers
(`"250000.00"`, never `250000.00`). IEEE-754 double-precision cannot represent every decimal
exactly, and JSON parsers across languages silently coerce. This is the wire-format expression of
`TXN-011`.

**EVG-004** — Every monetary object **MUST** be accompanied by an ISO-4217 `currency` at the
enclosing scope. A bare amount **MUST NOT** appear (TXN-011).

**EVG-005** — Timestamps **MUST** be RFC 3339 with an explicit `Z` offset. Local times and naive
timestamps **MUST NOT** be published.

**EVG-006** — Identifiers **MUST** be strings, never integers, even when the underlying storage
uses a numeric key. Numeric ids leak database implementation and break when a context migrates to
UUID/ULID.

### 1.3 Transport Binding

**EVG-007** — Structured mode **MUST** be used: the full CloudEvent is the message body, with
`content-type: application/cloudevents+json`. Binary mode (attributes as broker headers) **MUST
NOT** be used — it fragments the envelope across two places and complicates the outbox
`payload`/`headers` split.

| Broker | Binding requirements |
|---|---|
| **RabbitMQ** | Exchange `<context>.events` (topic). Routing key = `type`. `message_id` = `id`, `correlation_id` = `correlationid` for operator visibility. Publisher confirms **MUST** be enabled (REL-007). |
| **Kafka** | Topic `<context>.events`. **Partition key = `subject`** — this is what preserves per-aggregate ordering (REL-006). `acks=all` **MUST** be set (REL-007). |

**EVG-008** — The broker partition/routing key **MUST** be derived from `subject`
(= `aggregateId`). Random or round-robin partitioning destroys the per-aggregate ordering
guarantee that `REL-006` promises and `REL-027` consumers rely on.

---

## 2. Naming

**EVG-010** — Event `type` **MUST** be `<context>.<aggregate>.<past-tense-verb>.v<MAJOR>`,
lowercase, dot-separated, with hyphens inside multi-word segments (COM-033).

```
ordering.order.placed.v1
inventory.stock.reservation-rejected.v1
payment.payment.authorized.v2
shipping.shipment.delivery-failed.v1
```

**EVG-011** — The verb **MUST** be past tense. An event states a fact that has already happened
(COM-034).

| ✅ Correct | ❌ Rejected | Why |
|---|---|---|
| `order.placed` | `order.place` | Imperative — that is a command |
| `payment.authorized` | `payment.authorize` | Imperative |
| `stock.reserved` | `stock.reserving` | Not yet a fact |
| `shipment.dispatched` | `send-shipment-notification` | Names a *consumer's reaction*, not a fact (COM-035) |
| `order.cancelled` | `order.status-changed` | Too vague — consumers must parse the payload to know what happened |

**EVG-012** — Event names **MUST NOT** encode the consumer or the reaction. `order.placed` is
correct; `notify-warehouse-of-order` couples the publisher to a consumer and violates COM-035.

**EVG-013** — `<aggregate>` **MUST** be the aggregate root name from
[`contexts.md`](./contexts.md) § 3, singular and lowercase.

**EVG-014** — Only the **MAJOR** version appears in the `type`. Minor, additive revisions
**MUST NOT** change the `type`; they are distinguished by `dataschema` alone (see § 4).

---

## 3. The Event Registry

**EVG-020** — Every published event **MUST** have a JSON Schema committed under
`contracts/events/<context>/<aggregate>.<verb>.v<MAJOR>.json` (COM-040). An event published
without a registered schema **MUST** fail CI.

```
contracts/events/
├── ordering/
│   ├── order.placed.v1.json
│   ├── order.confirmed.v1.json
│   └── order.cancelled.v1.json
├── inventory/
│   ├── stock.reserved.v1.json
│   └── stock.reservation-rejected.v1.json
├── payment/
│   ├── payment.authorized.v1.json
│   └── refund.issued.v1.json
├── _shared/
│   ├── money.json                 # reusable definitions
│   └── address.json
└── README.md                      # ownership map + change procedure (COM-041)
```

### 3.1 Schema Requirements

**EVG-021** — Every schema **MUST** set `"additionalProperties": false` for **producer-side
validation**, and consumers **MUST NOT** enforce that constraint (see EVG-041, tolerant reader).
The producer is strict about what it emits; the consumer is lenient about what it accepts.

**EVG-022** — Every field **MUST** carry a `description`. A schema field whose meaning is not
written down will be interpreted differently by every consumer.

**EVG-023** — `required` **MUST** be declared explicitly and **MUST** be minimal. Every field
listed in `required` is a field that can never be removed without a MAJOR bump (§ 4.2).

```jsonc
// contracts/events/ordering/order.placed.v1.json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://contracts.example.com/events/ordering/order.placed.v1.json",
  "title": "ordering.order.placed.v1",
  "description": "A customer order has been created and the placement saga has started.",
  "type": "object",
  "additionalProperties": false,
  "required": ["orderId", "customerId", "placedAt", "currency", "lines", "totals"],
  "properties": {
    "orderId": {
      "type": "string",
      "description": "Platform order number. Stable, human-quotable, never reused."
    },
    "customerId": {
      "type": "string",
      "description": "Identity account id. Correlation only — no FK across contexts (ARC-022)."
    },
    "placedAt": {
      "type": "string", "format": "date-time",
      "description": "Business time the order was placed (UTC)."
    },
    "currency": {
      "type": "string", "pattern": "^[A-Z]{3}$",
      "description": "ISO-4217 code governing every amount in this event (EVG-004)."
    },
    "lines": {
      "type": "array", "minItems": 1,
      "description": "Ordered lines, price-snapshotted at placement (contexts.md §2).",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "required": ["sku", "name", "quantity", "unitPrice"],
        "properties": {
          "sku":       { "type": "string", "description": "Catalog SKU." },
          "name":      { "type": "string", "description": "Product name AS AT placement." },
          "quantity":  { "type": "integer", "minimum": 1, "description": "Units ordered." },
          "unitPrice": { "$ref": "../_shared/money.json#/$defs/amount" }
        }
      }
    },
    "totals": {
      "type": "object",
      "additionalProperties": false,
      "required": ["subtotal", "shipping", "discount", "tax", "grandTotal"],
      "description": "Server-computed totals (TXN-012). Never client-supplied.",
      "properties": {
        "subtotal":   { "$ref": "../_shared/money.json#/$defs/amount" },
        "shipping":   { "$ref": "../_shared/money.json#/$defs/amount" },
        "discount":   { "$ref": "../_shared/money.json#/$defs/amount" },
        "tax":        { "$ref": "../_shared/money.json#/$defs/amount" },
        "grandTotal": { "$ref": "../_shared/money.json#/$defs/amount" }
      }
    },
    "shippingAddress": { "$ref": "../_shared/address.json" }
  }
}
```

```jsonc
// contracts/events/_shared/money.json — EVG-003: amounts are STRINGS
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://contracts.example.com/events/_shared/money.json",
  "$defs": {
    "amount": {
      "type": "string",
      "pattern": "^-?\\d+\\.\\d{2}$",
      "description": "Decimal amount as a string with exactly 2 fractional digits (EVG-003). Currency is declared at the enclosing scope (EVG-004)."
    }
  }
}
```

### 3.2 Payload Design

**EVG-024** — Payloads **MUST** be self-contained for their stated purpose (COM-036). A consumer
forced to immediately call back to the publisher for basic context indicates an under-specified
event; the event **SHOULD** be enriched rather than the callback normalised.

**EVG-025** — Payloads **MUST NOT** be maximal either. Include what consumers need; exclude the
publisher's internal state. Every field published is a field that can never be removed without a
MAJOR version.

| Style | When to use | Trade-off |
|---|---|---|
| **Thin event** (ids + timestamps) | Internal, single trusted consumer, large aggregate | Forces a callback; couples consumer to publisher availability |
| **Event-carried state transfer** (full business snapshot) | **Default for cross-context events** | Larger payload; the snapshot is immutable history, which is usually correct |
| **Delta event** (changed fields only) | High-frequency updates | Consumers must hold prior state; ordering becomes critical (REL-027) |

**EVG-026** — Cross-context events **SHOULD** use event-carried state transfer. It is what lets
Notification render an order confirmation without querying Ordering, and what lets a consumer
process an event about an entity that has since been deleted.

**EVG-027** — Events **MUST NOT** carry: card data (SEC-001), passwords, secrets, API keys, or
raw authentication tokens. Events crossing a context boundary **SHOULD NOT** carry PII beyond
what the consumer demonstrably needs (SEC-012).

**EVG-028** — Where a consumer genuinely needs PII (Notification needs an email address), the
event **MAY** carry it, and the need **MUST** be recorded in the schema `description`. PII
carried "just in case" **MUST NOT** be included.

**EVG-029** — Events **MUST NOT** carry mutable references that will be resolved later at display
time. `productName` is snapshotted at publication; a consumer **MUST NOT** be expected to resolve
`productId → name` when rendering historical data (contexts.md § 2 snapshot rule).

---

## 4. Schema Evolution

> The governing reality: **producers and consumers deploy independently, and you do not control
> the order.** At any moment during a rollout, an old producer is emitting to a new consumer and
> a new producer is emitting to an old consumer. Both directions must work.

### 4.1 Required Compatibility Mode

**EVG-030** — Every event schema **MUST** maintain **FULL compatibility** within a major version:
both backward *and* forward compatible.

| Mode | Guarantees | Meaning here |
|---|---|---|
| **Backward** | New consumer reads old events | A consumer deployed today handles messages produced last month, and anything still sitting in a queue or DLQ |
| **Forward** | Old consumer reads new events | A consumer not yet redeployed survives the producer's rollout |
| **FULL** | Both | **REQUIRED** (EVG-030) |

**EVG-031** — FULL compatibility is required because the platform has: independent deployment
per context (ARC-024), at-least-once delivery with retained DLQs (RES-030) that may replay
week-old messages, and consumers outside the deploying team's control. Any weaker mode assumes a
coordinated deployment that this architecture explicitly does not have.

### 4.2 Change Classification

**EVG-032** — Every schema change **MUST** be classified before merge. CI enforces this
(EVG-050); reviewer judgement is not the control.

#### ✅ Non-breaking — same major version, `dataschema` revision only

| Change | Condition |
|---|---|
| Add an **optional** field | **MUST NOT** be added to `required`; **MUST** have a documented default or be safely absent |
| Add a value to an enum | **Only if** consumers are specified to tolerate unknown values (EVG-042) |
| Relax a constraint | Widen `maxLength`, lower `minimum`, loosen a `pattern` |
| Add a new event type | Entirely new `type`; no existing consumer affected |
| Improve a `description` | Documentation only |
| Make a required field optional | Old consumers still receive it; new ones tolerate absence |

#### ❌ Breaking — new **MAJOR** version required

| Change | Why it breaks |
|---|---|
| Remove a field | Old consumers read it; absence is undefined behaviour |
| Rename a field | Removal + addition; strictly worse because it looks harmless |
| Change a field's type | `string` → `integer` fails deserialization outright |
| Add a **required** field | Old producers cannot supply it; validation fails |
| Narrow a constraint | Existing valid values become invalid |
| Remove an enum value | Old producers still emit it |
| Change the meaning of a field without changing its name | **The most dangerous change of all** — nothing fails, and every consumer is silently wrong |
| Change the unit or scale | `"amount"` in dong → cents. Nothing errors; every number is off by 100× |

**EVG-033** — A semantic change **MUST** be treated as breaking even when the shape is
byte-identical. Redefining `totals.discount` from "amount deducted" to "percentage" while keeping
the type `string` **MUST** be a MAJOR bump. Type-checking cannot catch it, so governance must.

**EVG-034** — Renaming **MUST NOT** be done in place. To rename, add the new field, publish both
for a full deprecation cycle (§ 5), then remove the old one in the next MAJOR version.

### 4.3 Producing a New Major Version

**EVG-035** — A MAJOR bump **MUST** follow this sequence. Skipping the dual-publish window breaks
every consumer that has not yet redeployed.

```
Phase 1 — ANNOUNCE          Register order.placed.v2 schema. Notify consumers (COM-041).
                            v1 remains the only thing published.
                                        │
Phase 2 — DUAL PUBLISH      Producer publishes BOTH v1 and v2 for every occurrence.
  (minimum 30 days)         Both carry the SAME correlationid but DIFFERENT ids —
                            so a consumer subscribed to both would process twice.
                            ⚠ Each consumer MUST subscribe to exactly one (EVG-037).
                                        │
Phase 3 — MIGRATE           Consumers move to v2 at their own pace. Producer tracks
                            v1 consumer count via broker metrics.
                                        │
Phase 4 — DEPRECATE         v1 marked deprecated with a removal date. Alert on any
                            remaining v1 consumer.
                                        │
Phase 5 — REMOVE            v1 publication stops. Schema retained in the registry,
                            marked withdrawn — never deleted (§5.3).
```

**EVG-036** — The dual-publish window **MUST** be at least **30 days**, and **MUST NOT** end
while any consumer is still subscribed to the old version. Broker consumer-group metrics are the
authority on that, not an assumption.

**EVG-037** — During dual publish, a consumer **MUST** subscribe to exactly one version. A
consumer bound to both receives two messages with different `id` values for one business fact —
the inbox (REL-021) will not deduplicate them, and the effect happens twice.

**EVG-038** — Both versions **MUST** be written to the outbox in the same transaction as the
state change (REL-002). Dual publish does not relax atomicity.

**EVG-039** — The producer **MUST** emit a metric per published version so Phase 4 is driven by
data. Removing v1 because "it's probably fine by now" is how a quiet consumer breaks.

### 4.4 Tolerant Reader

**EVG-040** — Every consumer **MUST** be implemented as a **tolerant reader**: ignore unknown
fields, tolerate unknown enum values, and never fail on additive change.

**EVG-041** — Consumers **MUST NOT** deserialize with strict/exhaustive settings. In .NET, this
means `JsonSerializerOptions` **MUST NOT** set `UnmappedMemberHandling.Disallow`. A consumer that
throws on an unknown field turns every producer's additive change into an outage.

```csharp
// ✅ EVG-040/041 — tolerant reader
private static readonly JsonSerializerOptions Tolerant = new()
{
    PropertyNameCaseInsensitive = true,
    UnmappedMemberHandling      = JsonUnmappedMemberHandling.Skip,   // ignore unknown fields
    NumberHandling              = JsonNumberHandling.AllowReadingFromString,
};

// ❌ FORBIDDEN — violates EVG-041; the next additive change takes this consumer down
// UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
```

**EVG-042** — Unknown enum values **MUST** be handled explicitly, never by an exhaustive switch
that throws. The consumer **MUST** either map to a documented fallback or park the message —
it **MUST NOT** crash-loop into the DLQ over a value the producer legitimately added.

```csharp
// ✅ EVG-042 — a value added by a newer producer must not crash an older consumer
var status = incoming.Status switch
{
    "Authorized" => PaymentStatus.Authorized,
    "Captured"   => PaymentStatus.Captured,
    "Declined"   => PaymentStatus.Declined,
    _            => LogAndIgnore(incoming.Status)   // NOT: throw new ArgumentOutOfRangeException
};
```

**EVG-043** — Consumers **MUST** bind only the fields they actually use. Mirroring the producer's
full payload into a consumer-side DTO creates coupling to fields the consumer does not care
about, and makes irrelevant changes look breaking.

**EVG-044** — Consumers **MUST** tolerate out-of-order and duplicate delivery (REL-020, REL-027).
Schema compatibility solves shape; it does not solve ordering.

---

## 5. Deprecation Lifecycle

**EVG-045** — An event version **MUST** progress through: `Active → Deprecated → Withdrawn`.
A version **MUST NOT** jump straight to removal.

| State | Meaning | Producer | Consumer |
|---|---|---|---|
| **Active** | Current | Publishes | May subscribe |
| **Deprecated** | Replacement exists; removal dated | Still publishes | **MUST** migrate before the removal date |
| **Withdrawn** | No longer published | Stopped | Any remaining subscriber is broken |

**EVG-046** — Deprecation **MUST** be declared in the schema itself, so it is visible to anyone
reading the contract rather than only to whoever read the announcement email.

```jsonc
{
  "title": "ordering.order.placed.v1",
  "deprecated": true,
  "x-deprecation": {
    "since":       "2026-09-03",
    "removalDate": "2026-12-03",          // EVG-047: MUST be ≥ 90 days out
    "replacedBy":  "ordering.order.placed.v2",
    "reason":      "totals.tax split into taxLines[] for multi-jurisdiction VAT",
    "migration":   "docs/migrations/order-placed-v1-to-v2.md"
  }
}
```

**EVG-047** — The removal date **MUST** be at least **90 days** after deprecation is announced,
and **MUST NOT** fall inside a peak sales period (Black Friday, Tết, 11.11).

**EVG-048** — Withdrawn schemas **MUST** be retained in the registry, marked withdrawn. They
**MUST NOT** be deleted: DLQ messages, event-store replays, and audit investigations reach back
years, and a message without a schema cannot be interpreted.

**EVG-049** — A withdrawn `type` **MUST NOT** be reused for a different meaning, ever.

---

## 6. Automated Enforcement

**EVG-050** — CI **MUST** fail a PR that changes an event schema incompatibly without a MAJOR
version increment (COM-042). This check is mandatory and **MUST NOT** be advisory.

```yaml
# .github/workflows/contracts.yml — EVG-050
name: contracts
on:
  pull_request:
    paths: ['contracts/**']

jobs:
  event-compatibility:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }

      # EVG-020: every published event has a registered schema
      - name: Schemas are valid JSON Schema 2020-12
        run: npx ajv-cli compile -s "contracts/events/**/*.json" --spec=draft2020

      # EVG-032: classify the change; fail if breaking without a major bump
      - name: Event schema compatibility (FULL)
        run: ./scripts/check-event-compat.sh origin/${{ github.base_ref }}

      # COM-025 / COM-042: protobuf wire compatibility
      - name: Protobuf breaking-change check
        run: |
          npx @bufbuild/buf breaking contracts/proto \
            --against ".git#branch=origin/${{ github.base_ref }},subdir=contracts/proto"

      # EVG-010: naming convention
      - name: Event naming convention
        run: ./scripts/check-event-naming.sh
```

**EVG-051** — Producers **MUST** validate outgoing events against their registered schema in
**non-production** environments, and **SHOULD** sample-validate in production. A producer emitting
messages that fail its own schema is publishing a contract it does not honour.

**EVG-052** — Contract tests **MUST** exist for every producer/consumer pair (QAG-007). The
producer test asserts "I emit what the schema says"; the consumer test asserts "I survive every
example the schema permits, including ones with unknown fields added."

**EVG-053** — The registry **MUST** include at least one **golden sample** message per event
version, committed alongside the schema, used by both sides' contract tests.

```
contracts/events/ordering/
├── order.placed.v1.json              # schema
└── samples/
    ├── order.placed.v1.minimal.json  # required fields only
    ├── order.placed.v1.full.json     # every optional field populated
    └── order.placed.v1.forward.json  # + unknown fields, to prove EVG-040 tolerance
```

---

## 7. Event Catalog

**EVG-054** — This catalog **MUST** be updated in the same PR that adds or changes an event.
It is the index consumers read first. Ownership per [`contexts.md`](./contexts.md) § 3.

| Event `type` | Owner | Status | Consumers | Schema |
|---|---|---|---|---|
| `identity.account.registered.v1` | Identity | Active | Notification | `events/identity/account.registered.v1.json` |
| `identity.account.email-verified.v1` | Identity | Active | Notification | `events/identity/account.email-verified.v1.json` |
| `identity.account.password-reset-requested.v1` | Identity | Active | Notification | `events/identity/…` |
| `identity.account.locked.v1` | Identity | Active | Notification | `events/identity/…` |
| `identity.account.deactivated.v1` | Identity | Active | Ordering, Notification | `events/identity/…` |
| `identity.account.role-assigned.v1` | Identity | Active | *(none yet)* | `events/identity/…` |
| `catalog.product.published.v1` | Catalog | Active | Inventory, Notification, Search | `events/catalog/…` |
| `catalog.product.unpublished.v1` | Catalog | Active | Ordering, Search | `events/catalog/…` |
| `catalog.product.content-updated.v1` | Catalog | Active | Search | `events/catalog/…` |
| `catalog.product.archived.v1` | Catalog | Active | Inventory, Search | `events/catalog/…` |
| `catalog.variant.price-changed.v1` | Catalog | Active | Ordering | `events/catalog/…` |
| `ordering.order.placed.v1` | Ordering | Active | Inventory, Payment, Notification | `events/ordering/order.placed.v1.json` |
| `ordering.order.confirmed.v1` | Ordering | Active | Shipping, Notification | `events/ordering/…` |
| `ordering.order.cancelled.v1` | Ordering | Active | Inventory, Payment, Shipping, Notification | `events/ordering/…` |
| `ordering.order.rejected.v1` | Ordering | Active | Notification | `events/ordering/…` |
| `ordering.order.return-requested.v1` | Ordering | Active | Inventory, Payment | `events/ordering/…` |
| `inventory.stock.reserved.v1` | Inventory | Active | Ordering | `events/inventory/…` |
| `inventory.stock.reservation-rejected.v1` | Inventory | Active | Ordering | `events/inventory/…` |
| `inventory.stock.committed.v1` | Inventory | Active | Ordering, Shipping | `events/inventory/…` |
| `inventory.stock.released.v1` | Inventory | Active | Ordering | `events/inventory/…` |
| `inventory.stock.level-changed.v1` | Inventory | Active | Catalog | `events/inventory/…` |
| `inventory.stock.low-threshold-reached.v1` | Inventory | Active | Notification | `events/inventory/…` |
| `inventory.stock.replenished.v1` | Inventory | Active | Catalog, Notification | `events/inventory/…` |
| `payment.payment.authorized.v1` | Payment | Active | Ordering | `events/payment/…` |
| `payment.payment.declined.v1` | Payment | Active | Ordering, Notification | `events/payment/…` |
| `payment.payment.captured.v1` | Payment | Active | Ordering, Notification | `events/payment/…` |
| `payment.payment.capture-failed.v1` | Payment | Active | Ordering | `events/payment/…` |
| `payment.payment.voided.v1` | Payment | Active | Ordering | `events/payment/…` |
| `payment.refund.issued.v1` | Payment | Active | Ordering, Notification | `events/payment/…` |
| `payment.reconciliation.discrepancy-detected.v1` | Payment | Active | Notification | `events/payment/…` |
| `shipping.shipment.created.v1` | Shipping | Active | Ordering, Notification | `events/shipping/…` |
| `shipping.shipment.dispatched.v1` | Shipping | Active | Ordering, Notification | `events/shipping/…` |
| `shipping.shipment.in-transit.v1` | Shipping | Active | Notification | `events/shipping/…` |
| `shipping.shipment.delivered.v1` | Shipping | Active | Ordering, Notification | `events/shipping/…` |
| `shipping.shipment.delivery-failed.v1` | Shipping | Active | Ordering, Notification | `events/shipping/…` |
| `shipping.shipment.returned.v1` | Shipping | Active | Ordering, Inventory, Payment | `events/shipping/…` |
| `notification.notification.sent.v1` | Notification | Active | Analytics | `events/notification/…` |
| `notification.notification.failed.v1` | Notification | Active | Ops alerting | `events/notification/…` |
| `notification.notification.bounced.v1` | Notification | Active | Identity | `events/notification/…` |

*(Catalog is seeded from `contexts.md` § 3. Rows are added as events are implemented; `Status`
tracks the EVG-045 lifecycle.)*

---

## 8. Checklist — Adding or Changing an Event

- [ ] Owning context confirmed against [`contexts.md`](./contexts.md) § 3 (ARC-010)
- [ ] `type` follows `<context>.<aggregate>.<past-tense-verb>.v<N>` (EVG-010, EVG-011)
- [ ] Name states a fact, not a reaction or a consumer (EVG-012)
- [ ] JSON Schema registered under `contracts/events/` (EVG-020)
- [ ] Every field has a `description` (EVG-022); `required` is minimal (EVG-023)
- [ ] Money is a string with currency at the enclosing scope (EVG-003, EVG-004)
- [ ] Timestamps are RFC 3339 UTC; ids are strings (EVG-005, EVG-006)
- [ ] No card data, secrets, or unjustified PII (EVG-027, EVG-028)
- [ ] Payload self-contained for its purpose, not maximal (EVG-024, EVG-025)
- [ ] Change classified: additive vs breaking (EVG-032); semantic changes treated as breaking (EVG-033)
- [ ] If breaking: MAJOR bump + dual-publish plan ≥ 30 days (EVG-035, EVG-036)
- [ ] If deprecating: `x-deprecation` in schema, removal ≥ 90 days out, not in peak season (EVG-046, EVG-047)
- [ ] Golden samples committed, including a forward-compatibility sample (EVG-053)
- [ ] Contract tests for producer and every consumer (EVG-052, QAG-007)
- [ ] Consumers verified as tolerant readers (EVG-040, EVG-041, EVG-042)
- [ ] Event catalog § 7 updated in this PR (EVG-054)
- [ ] Consuming context maintainer reviewed the PR (COM-041)

---

*Related: [`constitution.md`](./constitution.md) · [`contexts.md`](./contexts.md) ·
[`guidelines.md`](./guidelines.md) · [`templates/feature-spec-template.md`](./templates/feature-spec-template.md)*
