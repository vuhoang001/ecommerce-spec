# Implementation Plan: Product Catalog

**Branch**: `002-product-catalog` | **Date**: 2026-09-04 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-product-catalog/spec.md`

## Summary

Deliver the customer-facing read path of the catalogue: browse a category, open a product, search by
name, and filter by category and price range — with discount display fed by the Promotion module and
degraded gracefully when Promotion is unreachable.

Technical approach: a .NET 8 modular monolith, one deployable host process, with the Catalog module":"
Catalog-owned port over a proto-defined contract, served in-process today and by a gRPC client after
extraction. Search uses PostgreSQL `unaccent` + `pg_trgm` to satisfy the case- and
diacritic-insensitive partial match. Money is an integer minor-unit `Money` type end to end.

**Terminology**: the spec's *discount copy* (FR-014) is implemented as
`DiscountProjection` in `catalog.discount_projection`. One concept, two names — the spec keeps the
plain-language one because SPC-001 bars implementation names from `spec.md`.

Phase 0 found that FR-026 cannot be satisfied by a query Catalog is allowed to write — the
discounted price it must filter on belongs to Promotion. Catalog therefore maintains
`catalog.discount_projection`, its own filterable copy of the active discount per product, fed by
consuming `promotion.discount.changed.v1` through the inbox. That gives the inbox one real consumer;
the outbox has no publisher, because Catalog produces nothing in this feature. See research.md R1
and Complexity Tracking.

The catalogue is a public, unauthenticated read surface (FR-034), so every read path is rate limited
per caller (FR-035) and runs on two or more stateless instances behind a load balancer to meet the
99.9% availability target (FR-036, SC-015). The rate limit is held per instance and is therefore
approximate — making it exact would mean adding Redis, which STK-001 does not permit without an
amendment (research.md R11).

## Technical Context

**Language/Version**: C# 12 on .NET 8 (LTS)

**Primary Dependencies**: ASP.NET Core Minimal APIs (storefront read endpoints);
**Dapper** for every read path and EF Core for writes, as DAT-004 requires — reads never touch
the `DbContext`, and visibility on those reads comes from DAT-005's shared `CatalogVisibility`
fragment rather than the EF global query filter, which Dapper cannot see;
`Microsoft.AspNetCore.RateLimiting` — the .NET 8 built-in token-bucket limiter (FR-035);
`Microsoft.Extensions.Diagnostics.HealthChecks` (readiness and liveness, research.md R13); EF Core 8 with
Npgsql; Grpc.AspNetCore + Google.Protobuf + Grpc.Tools (cross-module read contract); MassTransit 8
with RabbitMQ transport (shared messaging infrastructure only in this feature); FluentValidation
(request validation); Serilog (structured logging for OBS-001)

**Storage**: PostgreSQL 16. Schema `catalog`, owned solely by `CatalogDbContext`. Extensions
`unaccent` and `pg_trgm` required for FR-017. Monetary columns are `bigint` minor units.

**Testing**: xUnit; NetArchTest.Rules for the architecture suite (ARC-001, ARC-002, COM-001, COM-004,
TXN-006, REL-001, TXN-002); Testcontainers for PostgreSQL and RabbitMQ in integration tests;
FluentAssertions; Respawn for per-test database reset

**Target Platform**: Linux container, single host process (modular monolith per STK-002 [withdrawn citation])

**Project Type**: Web service — HTTP/JSON storefront read API. No frontend in this repository.

**Performance Goals**: p95 under 300 ms server-side for a category listing, a search, and a detail
view at 200 requests/second, leaving budget for the client to meet SC-003's 1-second perceived
first page. Search p95 under 400 ms at 100,000 products.

**Constraints**: Catalog must render every page when Promotion is unreachable (FR-013, SC-008), so
the live discount read is bounded by a 200 ms timeout and never blocks a response — listings read the
projection and never call Promotion at all, and the readiness probe deliberately excludes Promotion
(research.md R13). No floating-point type may appear in any money path (TXN-006). A projected
discount older than 15 minutes is not displayed (FR-015). Every read path is anonymous (FR-034) and
rate limited per caller (FR-035). No single instance failure may interrupt reads (FR-036); 99.9%
monthly availability with recovery inside 15 minutes (SC-015, SC-016).

**Deployment shape**: two or more stateless host instances behind a load balancer, and managed
PostgreSQL with automated failover or a rehearsed sub-15-minute restore. The database is the
remaining single point of failure and is a deployment requirement this feature depends on rather
than delivers (research.md R12).

**Scale/Scope**: 100,000 active products, 1,000 categories, average 3 categories per product, 200
requests/second peak, 4 storefront read endpoints, 1 cross-module read contract. These match SC-003
exactly, which now states the scale its 1-second target is measured against.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design — see bottom of file.*

Assessed against constitution v3.0.0. Every rule with a bearing on this feature:

| Rule | Status | How this plan satisfies it |
|------|--------|----------------------------|
| SPC-001 | PASS | `spec.md` passed the keyword scan; every technology decision lives here in `plan.md`. |
| MOD-005 [withdrawn citation] | PASS | Adds the `catalog` module and the `Promotion.Contracts` assembly only. The four-module set is unchanged. |
| ARC-001 | PASS | `Catalog.Infrastructure` references `ECommerce.Promotion.Contracts` and nothing else of Promotion. |
| ARC-002 | PASS | `Catalog.Contracts` and `Promotion.Contracts` hold proto-generated types and port DTOs only — no entity, no `DbContext`, no handler. |
| ARC-003 | PASS | `Shared.Kernel` gets `Money`, `Result`, `IClock`, `PagedResult` — all pass the banking-app test. No `Product` or `Category` type enters shared. |
| DAT-001 | PASS | One `CatalogDbContext`, `HasDefaultSchema("catalog")`, mapping no table outside it. |
| DAT-002 | PASS | No foreign key leaves `catalog`. `retained_discount_result.product_id` references `catalog.product`, inside the schema. |
| COM-001 | PASS | `IPromotionPricingPort` is declared in `Catalog.Application` (consumer-owned) and implemented in `Catalog.Infrastructure` (outside the domain), over `promotion_pricing.proto`. |
| COM-002 | PASS | Call depth 1 — the Promotion adapter is the only cross-module call, and Catalog makes none while serving one. |
| COM-003 | PASS | The discount read happens before any transaction opens; the read path opens no write transaction at all. |
| COM-004 | PASS, vacuous | Catalog performs no cross-module write in this feature. |
| TXN-006 | PASS | `Money` wraps `long` minor units. Architecture test bans `float`, `double`, `decimal` in every money path; columns are `bigint`. |
| TXN-005 | N/A | No order exists in this feature. The rule binds the Order module. |
| REL-001 | PASS, vacuous | No handler publishes, because nothing publishes. The architecture test banning `IBus` outside the relay runs regardless. |
| REL-002 | PASS | Relay drains with `FOR UPDATE SKIP LOCKED`; two concurrent relays publish each row once. Verified against real PostgreSQL even though no Catalog row reaches it (research.md R7). |
| REL-003 | PASS | The `promotion.discount.changed.v1` consumer deduplicates on `(message_id, consumer)` in the same transaction as the projection update. |
| REL-004 | PASS | The consumer is order-independent: it applies an update only when the message's `occurred_at` is newer than the projection row's, so reverse delivery converges to the same state. |
| REL-005 | PASS | Tolerant reader — a contract test delivers a payload carrying an added field. |
| COM-006 | PASS | The envelope is asserted at the consumer boundary; REL-003's key depends on `message_id`. |
| COM-008 | PASS | `promotion.discount.changed.v1` is versioned in its name and checked by the CI compatibility gate. |
| TXN-001, TXN-002 | PASS | Read-only path. `TransactionScope` is banned by architecture test regardless. |
| TXN-003, TXN-006 | N/A | No saga in this feature. |
| PRM-001 [withdrawn citation] | PASS | FR-011 — Catalog never calculates a discount. The port is read-only; the adapter has no write method. |
| PRM-002 [withdrawn citation], PRM-004 [withdrawn citation] | N/A | Promotion type logic belongs to the Promotion module. |
| PRM-003 [withdrawn citation] | PASS | The port returns a discriminated result — discount, rejection reason, or unavailable. There is no null and no silent skip. |
| QAG-001 | PASS | Every task in `tasks.md` will pair a failing test with its implementation, in that commit order. |
| QAG-002, QAG-004, QAG-005 | **PARTIAL** | Domain invariant tests and one test per acceptance criterion apply and are planned. Idempotency tests for money and order writes, and concurrency tests for stock, have no write path in this feature — see Complexity Tracking. |
| OBS-001 | PASS, adapted | The rule names promotion application and rejection. Catalog logs every discount result it displays, every rejection reason, and every fall back to a retained or undiscounted price, with the product identifier and reason code. |
| FR-034, SC-013 | PASS | Every endpoint is anonymous. No read path reads a customer identity, and none is defined. |
| FR-035, FR-037, SC-014 | PASS, approximate | Token bucket per caller address, per instance, budget divided by instance count. Rejections carry the reason code and `Retry-After` (FR-029). The limiter runs before the handler, so FR-001's visibility filter is never skipped under load. Precision limits are recorded in Complexity Tracking. |
| FR-036, SC-015, SC-016 | PASS, with a deployment dependency | Stateless instances behind a load balancer; concurrent relays and consumers are already safe under REL-002 and REL-003. PostgreSQL failover is a deployment requirement, not something this feature builds (research.md R12). |
| STK-001 | PASS | No component is added to the stack. The limiter is in the framework and the health checks are in the framework, so no the Governance amendment clause amendment is needed. Making the rate limit exact would need Redis and therefore an amendment — deliberately not taken. |
| DAT-004 | PASS | All four read paths execute through Dapper; no `*Query` takes a `DbContext`. Enforced by `Dat004ReadWriteSeparationTests`. |
| DAT-005 | PASS | Read visibility comes from the shared `CatalogVisibility` fragment. Enforced by `Dat005VisibilityFragmentTests`, which also forbids a hand-written clause at any call site. |
| DAT-006 | PASS | `scripts/check-sql-schemas.sh` scans raw SQL for schema-qualified names outside the owning module. |
| ARC-004 | PASS | `Product.Create` takes an injected timestamp; the domain reads no clock and mints no identifier. Enforced by `Arc004NoAmbientClockOrIdTests`. |
| ARC-005 | PASS | Deviations are recorded in `architecture-burndown.md` with an owner and a closing condition. |
| REL-007 | PASS | A Promotion outage never blocks a catalogue read: FR-013 falls back to the discount copy, and readiness deliberately excludes Promotion. |
| SEC-006 | PASS | Every input is validated server-side at the boundary — price range, keyword, message envelope, route constraints. |
| SEC-001 | N/A | No credential is created, stored or verified by this feature. Password rules bind whichever feature owns authentication. Recorded in `architecture-burndown.md`. |
| SEC-002 | N/A | No credential storage exists here, so there is nothing to hash. Recorded in `architecture-burndown.md`. |
| SEC-003 | N/A for authentication, **concern met anyway** | No account response exists. The underlying requirement — a response must not disclose whether an identifier exists — is satisfied by FR-002 and asserted byte-for-byte by `ProductDetailVisibilityTests`. |
| SEC-004 | N/A | The catalogue is anonymous (FR-034, SC-013) and exposes no per-resource permission, so there is no role or resource to check. Revisit the moment any endpoint reads a customer identity. |
| SEC-005 | N/A | No security-relevant event occurs on an anonymous read path. Promotion decisions are logged under OBS-001, which is an operational record, not a security one. |
| QAG-003 | PASS | Domain tests reference no infrastructure package. |
| QAG-006 | PASS | Every infrastructure suite runs against real PostgreSQL in Testcontainers, never a fake. |
| DEP-001 | PASS | `src/Host/ECommerce.Host/Dockerfile` builds the deployable inside the image — restore and publish happen in the build stage, never on the runner. Enforced by `scripts/check-deployable-images.sh` and the `backend-image` CI job. |
| DEP-002 | PASS | The `backend-image` job installs no .NET SDK and runs no `dotnet` command on the runner, so a green result proves the image builds from the backend source tree alone, without the frontend's toolchain. |
| UIX-001 | N/A | No frontend exists in this repository. The rule binds the frontend feature. |
| UIX-002 | PASS, partial | The OpenAPI contract is checked in and `OpenApiContractConformanceTests` fails if a documented path is unroutable or a route is undocumented. The host does not yet *emit* the document, so a generated client is validated against the contract rather than against generated output. |
| UIX-003 | PASS, enabling | Every monetary value crosses the wire as `amountMinor` (integer) plus a currency code, never a decimal — which is what makes client-side rendering without arithmetic possible. JavaScript's single float numeric type is why `TXN-006`'s guarantee would otherwise be lost at this boundary. |
| UIX-004, UIX-005 | N/A | Keyboard operability and the component library bind the frontend feature. |
| COM-005 | PASS | `promotion.discount.changed.v1` is a past-tense fact, broadcast, with any number of consumers. No command is defined by this feature. |
| COM-007 | PASS | The event name follows `<context>.<aggregate>.<past-tense-verb>.v<N>`. |
| DAT-003 | PASS | The discount copy snapshots a Promotion fact at the time of the event and is never re-read from Promotion for a historical record. |
| REL-006 | **OPEN** | No dead-letter queue is configured and no replay procedure is written. Recorded as `BD-003` in `architecture-burndown.md`. |
| TXN-004 | WITHDRAWN | Superseded by `TXN-006` in constitution v2.0.0. Cited here only to record that the withdrawal was noticed. |
| GATE-001 | PASS | Architecture suite, migration scanner, and contract tests all run in CI and block the merge. |
| GOV-007 [withdrawn citation] | N/A | This feature touches no file in the promotion module beyond its `.Contracts` proto. |

**Gate result: PASS with two items carried to Complexity Tracking.** No rule is violated. Two are
satisfied vacuously in a way the reviewer should see rather than discover.

## Project Structure

### Documentation (this feature)

```text
specs/002-product-catalog/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── README.md
│   ├── promotion_pricing.proto
│   └── catalog-storefront.openapi.yaml
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── Host/
│   └── ECommerce.Host/                        # the deployable process; composition root,
│                                              # rate limiter (FR-035), health endpoints (R13)
├── Modules/
│   ├── Catalog/
│   │   ├── ECommerce.Catalog.Contracts/       # events + port DTOs; no entity, no EF type (ARC-002)
│   │   ├── ECommerce.Catalog.Domain/          # Product, Category, ProductStatus, invariants
│   │   ├── ECommerce.Catalog.Application/     # use cases + IPromotionPricingPort (COM-001)
│   │   └── ECommerce.Catalog.Infrastructure/  # CatalogDbContext, migrations, promotion adapter
│   └── Promotion/
│       └── ECommerce.Promotion.Contracts/     # promotion_pricing.proto only; module body is a later feature
└── Shared/
    ├── ECommerce.Shared.Kernel/               # Money, Result, IClock, PagedResult (ARC-003)
    └── ECommerce.Shared.Messaging/            # outbox table, relay, inbox consumer base (REL-001, REL-002, REL-003)

tests/
├── ECommerce.ArchitectureTests/               # ARC-001/002/005, COM-001/004, TXN-006, PRM-001 [withdrawn citation],
│                                              # REL-001, TXN-002
├── Catalog/
│   ├── ECommerce.Catalog.UnitTests/           # domain invariants, price-range matching
│   ├── ECommerce.Catalog.IntegrationTests/    # endpoints against real PostgreSQL (Testcontainers)
│   └── ECommerce.Catalog.ContractTests/       # promotion port contract, incl. unreachable behaviour
├── Shared/
│   ├── ECommerce.Shared.Kernel.Tests/         # Money — a shared primitive, tested where it lives
│   └── ECommerce.Shared.Messaging.Tests/      # relay concurrency (REL-002), inbox replay (REL-003)
├── ECommerce.Catalog.ResilienceTests/         # rate limit rejection shape, readiness excludes
│                                              # Promotion, seed runs once across instances
└── performance/                               # load test for SC-003 and SC-004

docs/
├── context-map.md                             # module boundaries and event ownership (not a constitution rule — a plan-level statement)
├── quickstart-results.md                      # the recorded quickstart run (T103)
├── reviews/port-review-checklist.md           # COM-002 call depth, COM-003 transaction isolation
└── runbooks/
    ├── catalog-messaging-replay.md            # REL-006 dead-letter and replay procedure
    └── catalog-recovery-drill.md              # SC-016 recovery evidence

architecture-burndown.md                       # ARC-005 known deviations, with closing conditions
```

**Structure Decision**: One solution, one host image run as two or more identical instances. `docs/`
holds the artifacts that satisfy rules no test can check — the port review checklist for COM-002 and
COM-003, and the recovery drill record for SC-016. Each module is four assemblies —
`Contracts`, `Domain`, `Application`, `Infrastructure` — so ARC-001 is checkable by assembly
reference and extraction later moves whole projects rather than splitting them. Only Catalog is
built out in this feature; Promotion appears as its `.Contracts` assembly alone, because Catalog
needs the proto contract to compile against and nothing more. User and Order are untouched.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Outbox table and relay built with zero Catalog publishers | Requested in the plan input, and REL-001/REL-002 must be proven before the first module publishes so that publisher inherits a working relay rather than writing one under deadline. | Deferring the outbox to the Order feature is simpler and costs this feature nothing, because Catalog emits no event the spec asks for. Rejected because the input asked for it. The inbox, by contrast, is no longer optional — research.md R1 gives it a real consumer. **Overrule this and I cut the outbox and relay only; the inbox stays.** |
| The rate limit is approximate, not exact | FR-035 needs a per-caller limit and FR-036 needs several instances. An exact shared counter needs Redis, which STK-001 excludes without a the Governance amendment clause amendment (research.md R11). Per-instance budgets divided by instance count keep the stack unchanged. | An exact Redis-backed limiter is the honest answer if the limit ever has to be precise, and it costs a constitution amendment before it can be written. Rejected now because the limit exists to stop scraping, not to meter a quota, where a factor-of-N error under uneven balancing does not defeat the purpose. **Overrule this and the first step is amending STK-001, not editing the plan.** |
| PostgreSQL failover is required but not built here | SC-015's 43-minute monthly budget and SC-016's 15-minute recovery cap are both unreachable if a database failure means a manual restore. | Nothing simpler reaches the target — a single database with manual recovery fails SC-016 on the first incident. This is called out as a deployment dependency so it is provisioned deliberately rather than discovered during the first outage. |
| `catalog.discount_projection` holds a copy of Promotion-owned data | FR-026 requires filtering on the discounted price, and DAT-001 forbids Catalog from reading Promotion's tables. A local filterable copy is the only shape that satisfies both (research.md R1). | Filtering on the original price alone needs no copy and was the spec's option B, rejected by the answer to Question 1. Querying Promotion per filter request needs no copy either, and puts an unbounded cross-module read on the hot path, contradicting SC-008. |
| QAG-002, QAG-004, QAG-005 satisfied only in part | The rule mandates idempotency tests for money and order writes and concurrency tests for stock. This feature has no write path at all — stock is read (FR-005) and never changed, and no order exists. | Writing those tests anyway would mean inventing a write path the spec puts out of scope. The requirement transfers to the first feature that writes stock or orders, and is recorded here so the gap is deliberate rather than forgotten. |

## Post-Design Constitution Re-Check

Re-evaluated after Phase 1 produced `data-model.md` and `contracts/`.

- **ARC-002 holds under the generated code.** `promotion_pricing.proto` generates message and client
  types only into `Promotion.Contracts`; the adapter that consumes them lives in
  `Catalog.Infrastructure`, so no handler enters a `.Contracts` assembly.
- **TXN-006 holds through the wire format.** The proto carries `int64 amount_minor` plus a currency
  code; no `double` appears in the contract, which is where a money bug would otherwise enter from
  outside the architecture test's reach.
- **PRM-003 [withdrawn citation] holds through the contract shape.** `PricingResult` is a proto `oneof` over `applied`,
  `rejected`, and `unavailable` — the absent case is unrepresentable rather than merely forbidden.
- **DAT-001 holds after adding `discount_projection`.** It lives in `catalog`, is written only by
  Catalog's own consumer, and holds a copy rather than a reference — no foreign key crosses a schema
  (DAT-002).
- **One deviation surfaced by the design and carried forward**: the projection widens FR-014. The
  spec described retaining the last result *returned for a product*, which reads as a cache filled on
  view; a filter needs every discounted product, including ones nobody browsed to. It stays
  compliant with PRM-001 [withdrawn citation] — Catalog only reads from Promotion, never writes — and the copy is
  explicitly non-authoritative, carrying `retrieved_at` and the 15-minute display expiry of FR-015.
  **This widening needs the spec author's confirmation** (research.md R1).

- **Two findings the availability requirement forced out of the design, both resolved here**:
  - *Start-up seeding breaks under redundancy.* FR-031 seeds the discount projection at start-up;
    N instances starting together would each run a full seed. Guarded by a PostgreSQL advisory lock
    so one instance seeds, with an idempotent upsert so a lost lock costs duplicated work rather than
    a wrong projection (research.md R12).
  - *A naive readiness probe would convert a Promotion outage into a catalogue outage.* Readiness
    excludes Promotion by design; including it would mark every instance unready during a Promotion
    outage and defeat SC-008 exactly when it matters (research.md R13).
- **FR-037 holds through ordering.** The limiter runs as middleware ahead of every handler, so a
  refused caller never reaches a query — there is no path where load causes FR-001's visibility
  filter to be skipped.

**Re-check result: PASS.** No new violation. Two entries added to Complexity Tracking: the
approximate rate limit, and the PostgreSQL failover dependency.
