# Phase 0 Research: Product Catalog

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-04

No `NEEDS CLARIFICATION` markers entered this phase — the spec resolved both of its own. The
unknowns below came from the technology choices in the plan input and from one contradiction the
design surfaced.

---

## R1. Filtering on a price the Catalog module does not own — the blocking finding

**Problem**: FR-026 requires a price range to match a product's original price, its discounted
price, or both. The discounted price is computed by the Promotion module. DAT-001 forbids Catalog
from reading Promotion's tables, so no query Catalog can write has a discounted price to filter on.
Fetching discounts for a candidate page after filtering does not work either: a product priced
250,000 and discounted to 180,000 is absent from the candidate set for a 150,000–200,000 range, so
it can never be recovered.

**Decision**: Catalog maintains `catalog.discount_projection` — its own copy of the currently active
discount per product, filterable in SQL alongside `product.price_minor`. The projection is fed by
consuming `promotion.discount.changed.v1` through the inbox (REL-003), seeded and reconciled by a
periodic full read of the Promotion port.

**Rationale**: The projection is the only shape that satisfies FR-026 without violating DAT-001. It
also subsumes FR-014's retained discount result — one table serves both the filter and the
unreachable-Promotion fallback of FR-013, instead of two copies of the same fact drifting apart.

**Consequence the spec did not anticipate**: FR-014 described retaining "the most recent discount
result the Promotion feature returned", which reads as a per-product cache populated on view. The
projection is broader — it covers every discounted product whether or not anyone viewed it, because
a filter must see products the customer has not browsed to. This is a widening of FR-014 and should
be confirmed.

**Alternatives considered**:
- *Filter on original price only (the spec's option B)*: no projection, no messaging, one query. Was
  rejected by the answer to Question 1, not on technical grounds.
- *Ask Promotion for all active discounts on every filter request*: no stored copy, always current.
  Rejected — it puts an unbounded cross-module read on the hot path and makes filtering fail
  whenever Promotion is slow, which contradicts SC-008.
- *Promotion writes discounts into a shared table*: rejected outright by DAT-001 and PRM-001.

---

## R2. What the messaging infrastructure is actually for

**Decision**: The inbox has exactly one consumer in this feature —
`promotion.discount.changed.v1`, maintaining the R1 projection. The outbox is built with the relay
and its tests, and has **zero publishers**: Catalog produces no event any other module consumes,
because the authoring path is out of scope.

**Rationale**: R1 gives the inbox a genuine reason to exist, which it did not have when the plan was
first drafted. The outbox does not get one. It is built because the plan input asked for it and
because REL-001/REL-002 must be proven before the first publisher exists, not because this feature
sends anything.

**Alternatives considered**:
- *Poll Promotion on a timer instead of consuming events*: simpler, no broker, no inbox. Rejected as
  the primary mechanism because staleness would be bounded by the poll interval rather than by
  delivery, but retained as the reconciliation path below.
- *Defer messaging to the Order feature entirely*: still the cheaper option for the outbox
  specifically. Recorded in the plan's Complexity Tracking.

---

## R3. Case- and diacritic-insensitive partial name search

**Decision**: PostgreSQL `unaccent` and `pg_trgm` extensions. A generated column
`name_normalized = lower(unaccent(name))` carries a GIN trigram index; the query matches
`name_normalized LIKE '%' || lower(unaccent(@keyword)) || '%'`.

**Rationale**: Normalising both the stored name and the keyword through the same function gives
FR-017's requirement in both directions — a keyword without diacritics matches a name with them and
the reverse — from one index. `unaccent` must be wrapped in an `IMMUTABLE` function to be usable in
a generated column; this is a known, documented step, not a workaround.

**Alternatives considered**:
- *`citext` alone*: handles case, not diacritics. Insufficient for FR-017.
- *`to_tsvector` full-text search*: built for word matching, not the substring match FR-017
  specifies. "phe" would not match "cà phê" as a prefix-less infix.
- *`ILIKE '%x%'` with no index*: correct and unusable — a sequential scan over 100,000 products
  misses the p95 in the plan's Performance Goals.

---

## R4. Money representation

**Decision**: `Money` in `ECommerce.Shared.Kernel`, a readonly record struct over
`long AmountMinor` and a 3-letter currency code. PostgreSQL columns are `bigint`. Arithmetic lives
on the type; no bare `long` crosses a boundary as a price.

**Rationale**: MON-001 requires integer minor units and bans floating point. Wrapping the integer
gives the architecture test one type to assert on rather than a rule about primitive `long` fields
it cannot distinguish from a stock count. `Money` passes MOD-003's banking-app test unchanged.

**Note on the currency in the spec's examples**: prices of 50,000–200,000 read as VND, whose minor
unit is the dong itself — the minor-unit scale is 1, not 100. The type carries the scale per
currency rather than assuming cents.

**Alternatives considered**:
- *`decimal`*: correct to 28 digits and still banned by MON-001, which names integer types.
- *Bare `long`*: indistinguishable from any other count in an architecture test, and loses currency.

---

## R5. Cross-module read transport

**Decision**: `promotion_pricing.proto` defines the contract. `IPromotionPricingPort` is declared in
`Catalog.Application`. Its implementation today is an **in-process adapter** that calls Promotion's
handler directly using the proto-generated message types; the gRPC client replaces the adapter after
extraction, behind the same port.

**Rationale**: COM-001 requires the proto contract and the consumer-owned port, which this gives. It
does not require a network hop, and inside one process a loopback gRPC call adds serialisation and
an HTTP/2 stream for no isolation benefit — both modules already share a failure domain.

**This differs from the plan input**, which asked for gRPC. The proto contract and the port are
adopted exactly as asked; only the transport is deferred. Say so and the adapter becomes a
`GrpcChannel` client against a loopback listener instead — one class changes, nothing else.

**Alternatives considered**:
- *Loopback gRPC now*: proves the wire format earlier and pays serialisation on every request.
- *A plain C# interface with no proto*: violates COM-001.

---

## R6. Pagination

**Decision**: Offset pagination with a companion `COUNT(*)`, page size 24, hard maximum 100.

**Rationale**: FR-007 requires the total and the page position, which keyset pagination cannot give
without a second count anyway. At 100,000 products the count is index-only and within budget.

**Alternatives considered**:
- *Keyset*: better at deep offsets, cannot state a page position. Revisit if catalogue growth makes
  deep pages slow — noted, not needed now.

---

## R7. MassTransit outbox and REL-002's explicit locking

**Decision**: MassTransit 8's EF Core transactional outbox, configured for PostgreSQL, with the
outbox tables in the `catalog` schema. An integration test captures the SQL the delivery sweep emits
and asserts it contains `FOR UPDATE SKIP LOCKED`.

**Rationale**: REL-002 names the locking clause specifically, so the plan verifies it rather than
trusting the library's documented behaviour. Two concurrent relay instances running against a seeded
outbox must publish each row exactly once — that test is the real gate, and the SQL assertion is
what makes the failure legible when it breaks after a library upgrade.

**Alternatives considered**:
- *A hand-written relay*: full control over the SQL and one more piece of infrastructure to own.
  Reconsider if the captured SQL does not satisfy REL-002.

---

## R8. Architecture test tooling

**Decision**: NetArchTest.Rules, one test class per constitution rule, each named for its identifier
(`MOD_001_ModuleReferencesOnlyContracts`).

**Rationale**: GATE-004 requires reviews to cite identifiers; naming the tests after them makes a CI
failure cite the rule by itself. NetArchTest reads assembly references and type declarations
directly, which is exactly what MOD-001 and MOD-002 are stated in terms of.

**Alternatives considered**:
- *ArchUnitNET*: richer rule language, heavier. Either works; this choice is not load-bearing.
- *Roslyn analyzers*: catch violations in the editor and cost far more to write and maintain.

---

## R9. Enforcing product visibility

**Decision**: A global query filter on `CatalogDbContext` restricting every product read to
`status = Active`, plus an integration test per read path asserting that a Hidden and a
Discontinued product are absent.

**Rationale**: FR-001 applies to every listing, search, filter, and detail view. A filter applied at
each call site is one forgotten call site away from breaking SC-002; a global filter fails closed.

**Alternatives considered**:
- *Filtering per query*: explicit at every call site and easy to omit in the next feature.
- *A database view over active products*: also fails closed, and splits the model across two objects
  for a rule EF Core expresses in one line.

---

## R10. Test data and isolation

**Decision**: Testcontainers for PostgreSQL and RabbitMQ; Respawn to reset between tests; the
Promotion port served by a controllable fake in integration tests, with the contract test suite
running against both the fake and the real in-process adapter.

**Rationale**: FR-013's unreachable-Promotion behaviour and SC-008 can only be tested by making
Promotion fail on demand, which a fake gives and a real dependency does not. Running the same
contract suite against both keeps the fake honest.

---

## R11. Where the rate limit counter lives (FR-035, FR-037)

**Problem**: FR-035 requires a per-caller rate limit. FR-036 requires the catalogue to keep serving
when one instance fails, which means more than one instance. A counter held in each instance's memory
multiplies the effective limit by the instance count; a shared counter needs a shared store. The
obvious shared store is Redis — and STK-001 names the stack as .NET 8, PostgreSQL, and RabbitMQ,
so adding Redis is a constitution amendment under GOV-002, not a plan decision.

**Decision**: The rate limiter built into .NET 8 (`Microsoft.AspNetCore.RateLimiting`), a token
bucket partitioned by caller address, held in each instance's memory. The per-instance budget is the
total budget divided by the instance count. Rejections return the reason and a `Retry-After` value.
**The limit is approximate, and the plan says so rather than implying precision it does not have.**

**Rationale**: It adds no component to the stack, so no amendment is needed. The limit exists to stop
a scraper pulling 100,000 products, not to meter a paid quota — being off by a factor of the instance
count during uneven load balancing does not defeat that purpose. Precision would cost a stack change.

**Alternatives considered**:
- *Redis-backed distributed limiter*: exact across instances, and the honest recommendation if the
  limit ever needs to be precise. Requires amending STK-001 under GOV-002 first. Not taken now
  because nothing in the spec asks for precision.
- *Counters in PostgreSQL*: no new component, and a write on every read request — it turns the
  read-only path into a write path and spends the p95 budget on bookkeeping.
- *Rate limiting at the ingress proxy*: exact and central, and it moves FR-035's rejection shape out
  of the application, where FR-029 requires a specific reason code. Viable later as a first line of
  defence in front of the application limiter, not instead of it.

---

## R12. Redundancy, and what "a single instance fails" costs (FR-036, SC-015, SC-016)

**Decision**: Two or more stateless host instances behind a load balancer. No read path holds session
or in-memory state that a subsequent request depends on, so any instance can serve any request.

**What running N instances does to the rest of the design**:
- *Outbox relay*: every instance runs one. REL-002's `FOR UPDATE SKIP LOCKED` already makes
  concurrent relays safe, and the two-relay test already proves it. No change.
- *Projection consumer*: every instance consumes the same queue as competing consumers. REL-003's
  inbox deduplication makes duplicate delivery safe. No change.
- *Start-up seeding (FR-031)*: **this one does break.** N instances starting together would each run
  a full seed of the discount projection. Guarded by a PostgreSQL advisory lock so exactly one
  instance seeds; the seed itself is an idempotent upsert, so a lost lock costs duplicated work, not
  a wrong projection.
- *Rate limiter*: the only genuinely per-instance state. See R11.

**The remaining single point of failure is PostgreSQL.** A 99.9% monthly budget is roughly 43
minutes, and SC-016 caps a single outage at 15 minutes — neither is reachable if a database failure
means a manual restore. This requires managed PostgreSQL with automated failover, or a rehearsed
restore that completes inside 15 minutes. **This is a deployment requirement the feature depends on
and does not itself deliver.**

**Alternatives considered**:
- *A single instance with fast restart*: satisfies 99.9% only if every restart is under 43 minutes
  cumulative per month and nothing else fails. Fails FR-036 outright, which names instance failure.
- *Multi-region standby*: reaches beyond 99.9% and costs a data replication story the spec does not
  ask for.

---

## R13. What the readiness probe must not check (SC-008, SC-015)

**Decision**: Liveness reports whether the process is responsive. Readiness reports whether the
database is reachable and migrations are applied. **Readiness MUST NOT depend on the Promotion
feature being reachable.**

**Rationale**: This is the subtle failure the two requirements create together. SC-008 requires every
listing and detail view to render while Promotion is down. If readiness included a Promotion check,
a Promotion outage would mark every catalogue instance unready, the load balancer would remove them
all, and a degraded dependency would become a total catalogue outage — the precise outcome SC-008
exists to prevent. The design already tolerates Promotion being down (FR-013); the probe must be
told the same thing.

**Alternatives considered**:
- *Readiness includes every dependency*: the conventional default, and wrong here for the reason
  above. Worth stating explicitly because it is what someone will otherwise write.
- *No readiness probe*: no dependency coupling and no way for the load balancer to avoid an instance
  whose database connection is broken.

---

## R14. Proving recovery rather than asserting it (SC-016)

**Decision**: A documented drill, run before release and recorded with timings: kill one instance and
confirm no request fails; kill every instance and measure time to restored service; fail the database
over and measure time to restored service. SC-016 is met when all three complete inside 15 minutes.

**Rationale**: SC-016 says "demonstrated by a recovery exercise rather than asserted", so the
artifact that satisfies it is a recorded drill result, not a configuration value. The drill is also
the only way the PostgreSQL failover assumption in R12 gets tested before it matters.

**Alternatives considered**:
- *Asserting the target from the platform's SLA*: the platform's number covers the platform, not this
  feature's recovery path, its migrations, or its projection seeding.
