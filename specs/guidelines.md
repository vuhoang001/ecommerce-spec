# Development Guidelines — Code Structure & Workflow

**Version**: 1.0.0 | **Last Amended**: 2026-09-03
**Governed by**: [`specs/constitution.md`](./constitution.md) v2.0.0

> The constitution says *what* must be true. This document says *where the files go* and *what
> order you do things in*. When the two disagree, the constitution wins (§ 0.1 Authority).

---

## 1. Repository Layout

```
ecommerce-platform/
├── contracts/                      # Published cross-context contracts (COM-040)
│   ├── proto/<context>/v1/
│   ├── events/<context>/
│   │   └── samples/                # Golden samples (EVG-053)
│   └── README.md                   # Ownership map (COM-041)
│
├── docs/
│   ├── adr/                        # Architecture Decision Records (ARC-024, EXT-002)
│   │   └── 0001-modular-monolith-first.md
│   └── runbooks/                   # One per alert (OBS-032)
│
├── specs/                          # ← THIS framework
│   ├── constitution.md             # Authoritative rules
│   ├── contexts.md                 # Bounded context map
│   ├── event-governance.md         # Event schema & versioning
│   ├── guidelines.md               # This file
│   ├── templates/feature-spec-template.md
│   └── <NNN>-<feature-slug>/       # One directory per feature
│       ├── spec.md · plan.md · tasks.md
│       └── checklists/
│
├── src/
│   ├── BuildingBlocks/             # Technical primitives ONLY — no business concepts (ARC-012)
│   │   ├── Platform.Abstractions/  # AggregateRoot, IDomainEvent, Result<T>, IClock
│   │   ├── Platform.Messaging/     # Outbox, inbox, envelope, MassTransit wiring
│   │   ├── Platform.Observability/ # Correlation middleware, OTel setup (OBS-004)
│   │   └── Platform.Web/           # Error envelope (COM-012), idempotency filter (EDG-021)
│   │
│   └── Services/
│       └── <Context>/              # Identity · Catalog · Ordering · Inventory · Payment · …
│           ├── <Context>.Domain/
│           ├── <Context>.Application/
│           ├── <Context>.Infrastructure/
│           └── <Context>.Api/
│
├── tests/
│   ├── BuildingBlocks/
│   └── Services/<Context>/
│       ├── <Context>.Domain.Tests/          # No infrastructure (QAG-003)
│       ├── <Context>.Application.Tests/
│       ├── <Context>.Infrastructure.Tests/  # Testcontainers (QAG-008)
│       └── <Context>.Contract.Tests/        # QAG-007, EVG-052
│
├── scripts/                        # check-coverage.sh, check-event-compat.sh, …
├── tools/                          # Benchmarks, one-off utilities
├── Directory.Build.props           # Shared MSBuild settings, analyzers, warnings-as-errors
├── Directory.Packages.props        # Central package version management
├── Platform.sln                    # All contexts (modular monolith)
├── docker-compose.yml              # PostgreSQL, RabbitMQ, Redis, Jaeger for local dev
└── global.json                     # Pinned .NET SDK
```

**Why `BuildingBlocks` is dangerous** — it is the one place ARC-012 can be violated by accident.
A `Product`, `Money`-with-business-rules, or `OrderStatus` placed here couples every context to
every other. Before adding a type to `BuildingBlocks`, ask: *"would this still make sense in a
banking app?"* If no, it belongs in a context.

---

## 2. Service Structure — Clean Architecture

Every context has exactly four projects. The example below is `Ordering`; every context follows
the identical shape.

### 2.1 `<Context>.Domain` — the centre

Depends on **nothing** but the BCL and `Platform.Abstractions` (ARC-003, ARC-004).

```
Ordering.Domain/
├── Orders/                                 # One folder per aggregate
│   ├── Order.cs                            # ← Aggregate Root
│   ├── OrderLine.cs                        # Entity (no identity outside the aggregate)
│   ├── OrderNumber.cs                      # Value Object
│   ├── OrderStatus.cs                      # Enum / smart enum
│   ├── Money.cs                            # Value Object (TXN-011)
│   ├── ShippingAddress.cs                  # Value Object
│   ├── Events/                             # DOMAIN events — internal (COM-032)
│   │   ├── OrderPlacedDomainEvent.cs
│   │   └── OrderCancelledDomainEvent.cs
│   ├── Rules/                              # Named specifications
│   │   └── OrderMustHaveAtLeastOneLine.cs
│   └── Exceptions/
│       └── IllegalOrderTransitionException.cs
├── Carts/
├── Abstractions/                           # Ports the DOMAIN itself needs
│   ├── IOrderRepository.cs
│   └── IClock.cs                           # ARC-004 — never DateTime.UtcNow inline
└── AssemblyMarker.cs                       # Anchor for architecture tests (ARC-007)
```

**Forbidden in this project** — `using Microsoft.EntityFrameworkCore`, `using MassTransit`,
`using Microsoft.AspNetCore`, `HttpClient`, `DateTime.UtcNow`, `Guid.NewGuid()`,
`[Table]`/`[Column]` attributes, `async` methods that perform I/O.

```csharp
// Ordering.Domain/Orders/Order.cs
public sealed class Order : AggregateRoot<OrderNumber>
{
    private readonly List<OrderLine> _lines = [];
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    public OrderStatus Status { get; private set; }
    public Money GrandTotal { get; private set; }

    private Order() { }   // EF Core

    // Factory enforces the invariants; there is no other way to create a valid Order
    public static Order Place(CustomerId customerId, IReadOnlyList<OrderLine> lines,
                              ShippingAddress address, DateTimeOffset now)
    {
        if (lines.Count == 0)
            throw new DomainException("An order MUST have at least one line.");

        var order = new Order
        {
            Id = OrderNumber.Next(now),
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            ShippingAddress = address,
            PlacedAt = now,                      // injected, never read from the clock (ARC-004)
        };
        order._lines.AddRange(lines);
        order.GrandTotal = order.CalculateTotal();   // TXN-012: server-side, always

        order.Raise(new OrderPlacedDomainEvent(order.Id, customerId, order.GrandTotal, now));
        return order;
    }

    // EDG-032: the state machine lives HERE, not in the UI and not in the handler
    public void Confirm(DateTimeOffset now)
    {
        if (Status is not OrderStatus.AwaitingPayment)
            throw new IllegalOrderTransitionException(Status, OrderStatus.Confirmed);

        Status = OrderStatus.Confirmed;
        Raise(new OrderConfirmedDomainEvent(Id, now));
    }
}
```

### 2.2 `<Context>.Application` — use cases

Depends on `Domain` only (ARC-003). Declares ports; implements none of them.

```
Ordering.Application/
├── Orders/
│   ├── Commands/
│   │   └── PlaceOrder/                     # One folder per use case
│   │       ├── PlaceOrderCommand.cs
│   │       ├── PlaceOrderHandler.cs
│   │       ├── PlaceOrderValidator.cs      # SEC-031 — server-side validation
│   │       └── PlaceOrderResult.cs
│   └── Queries/
│       └── GetOrderById/
│           ├── GetOrderByIdQuery.cs
│           ├── GetOrderByIdHandler.cs      # MAY bypass the domain (ARC-032)
│           └── OrderDetailDto.cs
├── Sagas/
│   └── OrderPlacement/
│       ├── OrderPlacementSaga.cs           # State machine definition (SAG-010)
│       ├── OrderPlacementState.cs          # Persisted state (SAG-020)
│       └── OrderPlacementEvents.cs
├── Abstractions/                           # PORTS — implemented in Infrastructure (ARC-006)
│   ├── IUnitOfWork.cs
│   ├── ICatalogPriceReader.cs              # ACL interface; gRPC client lives in Infra
│   ├── IIntegrationEventPublisher.cs
│   └── IIdempotencyStore.cs
├── Behaviors/                              # Cross-cutting pipeline
│   ├── ValidationBehavior.cs
│   ├── TransactionBehavior.cs              # REL-002 — one transaction per command
│   └── LoggingBehavior.cs                  # OBS-021
└── AssemblyMarker.cs
```

```csharp
// Ordering.Application/Orders/Commands/PlaceOrder/PlaceOrderHandler.cs
internal sealed class PlaceOrderHandler(
    IOrderRepository orders,
    ICatalogPriceReader prices,          // port — gRPC adapter injected at runtime
    IIntegrationEventPublisher publisher, // port — writes to the OUTBOX, not the broker
    IUnitOfWork uow,
    IClock clock) : ICommandHandler<PlaceOrderCommand, PlaceOrderResult>
{
    public async Task<Result<PlaceOrderResult>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        // TXN-012: prices come from Catalog, never from the client
        var snapshot = await prices.GetSnapshotAsync(cmd.Items.Select(i => i.Sku), ct);
        if (snapshot.IsFailure) return snapshot.Error;

        var lines = cmd.Items.Select(i => OrderLine.From(i, snapshot.Value[i.Sku])).ToList();
        var order = Order.Place(cmd.CustomerId, lines, cmd.ShippingAddress, clock.UtcNow);

        orders.Add(order);

        // REL-002: enqueued to the outbox in THIS transaction. Nothing hits the broker here.
        publisher.Enqueue(order.DequeueDomainEvents().ToIntegrationEvents());

        await uow.SaveChangesAsync(ct);      // TransactionBehavior commits; both rows or neither
        return new PlaceOrderResult(order.Id.Value);
    }
}
```

### 2.3 `<Context>.Infrastructure` — adapters

Implements every port. Depends on `Application` + `Domain`; nothing depends on it (ARC-003).

```
Ordering.Infrastructure/
├── Persistence/
│   ├── OrderingDbContext.cs                # Schema "ordering" (ARC-021)
│   ├── Configurations/                     # ARC-005 — mapping lives HERE, not on entities
│   │   ├── OrderConfiguration.cs
│   │   └── OrderLineConfiguration.cs
│   ├── Repositories/OrderRepository.cs
│   ├── Migrations/
│   └── Outbox/
│       ├── OutboxMessage.cs                # REL-003 schema
│       └── OutboxPublisher.cs              # REL-004/005 relay, SKIP LOCKED
├── Messaging/
│   ├── Consumers/                          # One class per consumed event
│   │   ├── StockReservedConsumer.cs
│   │   └── PaymentAuthorizedConsumer.cs
│   ├── Inbox/InboxStore.cs                 # REL-021
│   └── Translators/                        # COM-032 — domain event → integration event
│       └── OrderEventTranslator.cs
├── External/                               # ACL for other contexts (ARC-014)
│   └── Catalog/
│       ├── GrpcCatalogPriceReader.cs       # implements ICatalogPriceReader
│       └── CatalogDtoMapper.cs             # external DTO → domain type; DTOs stop here
├── Idempotency/IdempotencyStore.cs         # EDG-021
└── DependencyInjection/InfrastructureExtensions.cs
```

### 2.4 `<Context>.Api` — presentation

```
Ordering.Api/
├── Endpoints/                              # Minimal API, one file per resource
│   ├── OrdersEndpoints.cs
│   └── CartsEndpoints.cs
├── Grpc/OrderingGrpcService.cs             # Generated from contracts/proto (COM-020)
├── Middleware/
│   ├── CorrelationIdMiddleware.cs          # OBS-001/002 — from Platform.Observability
│   ├── ExceptionHandlingMiddleware.cs      # COM-012 error envelope
│   └── IdempotencyMiddleware.cs            # EDG-020/021
├── HealthChecks/                           # OBS-033 — /health/live, /health/ready
├── Program.cs
├── appsettings.json
└── Dockerfile
```

### 2.5 Enforced Project References

```
Ordering.Api ──────────► Ordering.Application ──────► Ordering.Domain
     │                                                      ▲
     └──► Ordering.Infrastructure ─────────────────────────┘
                    │
                    └──► Ordering.Application

FORBIDDEN: Domain → anything · Application → Infrastructure · Api → another context
```

**ARC-007** — this is enforced by a test, not by discipline:

```csharp
[Fact]
public void Application_must_not_reference_Infrastructure()
    => Types.InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
        .ShouldNot().HaveDependencyOn("Ordering.Infrastructure")
        .GetResult().IsSuccessful.Should().BeTrue("ARC-003");

[Fact]
public void No_context_may_reference_another_contexts_domain()
    => Types.InAssembly(typeof(ApplicationAssemblyMarker).Assembly)
        .ShouldNot().HaveDependencyOnAny(
            "Catalog.Domain", "Inventory.Domain", "Payment.Domain",
            "Shipping.Domain", "Identity.Domain", "Notification.Domain")
        .GetResult().IsSuccessful.Should().BeTrue("ARC-010");
```

---

## 3. Naming Conventions

| Artifact | Convention | Example |
|---|---|---|
| Aggregate root | Singular noun | `Order`, `StockItem`, `Payment` |
| Value object | Singular noun, immutable `record` | `Money`, `OrderNumber`, `Sku` |
| Domain event | `<Aggregate><PastTenseVerb>DomainEvent` | `OrderPlacedDomainEvent` |
| Integration event class | `<Aggregate><PastTenseVerb>IntegrationEvent` | `OrderPlacedIntegrationEvent` |
| Integration event `type` | `<context>.<aggregate>.<verb>.v<N>` (EVG-010) | `ordering.order.placed.v1` |
| Command | `<Verb><Noun>Command` — imperative | `PlaceOrderCommand` |
| Command handler | `<Command>Handler` | `PlaceOrderHandler` |
| Query | `<Get\|List><Noun>Query` | `GetOrderByIdQuery` |
| Consumer | `<Event>Consumer` | `StockReservedConsumer` |
| Saga | `<Process>Saga` | `OrderPlacementSaga` |
| Repository port | `I<Aggregate>Repository` | `IOrderRepository` |
| DB schema | lowercase context name (ARC-021) | `ordering`, `inventory` |
| DB table | `snake_case`, plural | `order_lines`, `stock_items` |
| Test | `Method_scenario_expectedResult` | `Place_with_no_lines_throws` |
| Test for a rule | append the rule ID in the assertion message | `.Should().BeTrue("EDG-003")` |

**Ubiquitous Language rule** — the words in code **MUST** match the words in the spec's § 3
table. If the spec says `Reservation`, the class is `Reservation` — not `StockHold`, not
`Allocation`. A translation layer between spec vocabulary and code vocabulary means one of them
is wrong.

---

## 4. Developer Workflow — Spec to Production

> **The order is not negotiable.** Each step's output is the next step's input. Skipping ahead —
> writing the entity before the test, the handler before the contract — is how a spec and its
> implementation drift apart on day one.

```
 0. ORIENT      Read constitution + contexts.md. Locate the owning context.
 1. SPEC        Fill the feature spec template. Get it approved.
 2. CONTRACTS   Commit proto/event schemas FIRST. Consumers can start immediately.
 3. RED         Write failing tests from the acceptance criteria. Observe them fail.
 4. DOMAIN      Implement invariants until domain tests pass.
 5. APPLICATION Implement handlers until application tests pass.
 6. INFRA       Persistence, outbox, inbox, consumers. Testcontainers integration tests.
 7. API         Endpoints, gRPC, middleware.
 8. OBSERVE     Metrics, alerts, runbook, trace verification.
 9. REVIEW      PR against Appendix B of the constitution.
10. ROLLOUT     Flag, canary, monitor, remove flag.
```

### Step 0 — Orient *(15 minutes; skipping this costs days)*

- [ ] Read [`constitution.md`](./constitution.md) § 0 and the articles your feature touches
- [ ] Locate the owning context in [`contexts.md`](./contexts.md) § 3
- [ ] Confirm the capability belongs there (ARC-010). If it seems to need two contexts, **stop**
      and amend `contexts.md` § 7 first
- [ ] Check § 5 Anti-Patterns — is your instinctive design already on that list?

### Step 1 — Write the Spec

```bash
mkdir -p specs/007-stock-reservation
cp specs/templates/feature-spec-template.md specs/007-stock-reservation/spec.md
```

Fill **every** section. Approval gate (§ 18 of the template):
- Constitution Check (§ 13) has no unwaived deviation
- Acceptance criteria are executable — each one becomes a test name verbatim
- Owning **and** consuming context maintainers approved (COM-041)

> Spec Kit users: `/speckit-specify` scaffolds this, `/speckit-plan` produces the plan,
> `/speckit-tasks` the task list. All three read `.specify/memory/constitution.md`, which points
> at `specs/constitution.md` (G.5).

### Step 2 — Contracts First

Commit contracts **before** any implementation, in their own PR (COM-041). This unblocks
consuming teams immediately and forces the interface to be designed rather than emitted.

```bash
# gRPC
vim contracts/proto/inventory/v1/availability.proto

# Events + golden samples (EVG-020, EVG-053)
vim contracts/events/inventory/stock.reserved.v1.json
vim contracts/events/inventory/samples/stock.reserved.v1.minimal.json
vim contracts/events/inventory/samples/stock.reserved.v1.forward.json   # unknown fields

# Compatibility gate must pass (COM-042, EVG-050)
./scripts/check-event-compat.sh origin/main
npx @bufbuild/buf breaking contracts/proto --against '.git#branch=origin/main,subdir=contracts/proto'
```

- [ ] Event catalog updated (`event-governance.md` § 7, EVG-054)

### Step 3 — RED: Tests First *(QAG-001 — NON-NEGOTIABLE)*

Translate every acceptance criterion into a test **before** writing implementation, and **observe
it fail**. A test that has never failed proves nothing.

```csharp
// AC-4 from the spec → test, verbatim
[Fact]
public async Task Given_2_available_when_5_customers_reserve_then_exactly_2_succeed()
{
    await SeedStock(sku: "SKU-1234", available: 2);

    var results = await Task.WhenAll(
        Enumerable.Range(0, 5).Select(_ => Reserve("SKU-1234", quantity: 1)));

    results.Count(r => r.IsSuccess).Should().Be(2, "EDG-001: stock MUST NOT be oversold");
    results.Count(r => r.IsFailure).Should().Be(3);
    (await GetAvailable("SKU-1234")).Should().Be(0);
}
```

Minimum test set before implementation starts:

| Test | Required when | Rule |
|---|---|---|
| Domain invariants | Always | QAG-001, QAG-003 |
| Every AC from the spec | Always | QAG-001 |
| Idempotency (same key/message twice → one effect) | Any money or order write, any consumer | QAG-005 |
| Concurrency (N parallel → exactly M succeed) | Stock, vouchers, balances | QAG-006 |
| Saga compensation, **every branch** | Any saga | QAG-004 |
| Contract (producer emits per schema; consumer tolerates unknown fields) | Any published event | QAG-007, EVG-052 |

```bash
dotnet test   # MUST be red, for the right reason. Read the failures.
```

### Step 4 — Domain

Implement until the **domain** tests pass. Nothing else.

- [ ] Invariants enforced in the aggregate, not the handler (ARC-031)
- [ ] No I/O, no `DateTime.UtcNow`, no EF attributes (ARC-004, ARC-005)
- [ ] State transitions rejected inside the aggregate (EDG-032)
- [ ] Money is `decimal` + currency (TXN-011)
- [ ] Domain events raised on every significant change (COM-031)

```bash
dotnet test tests/Services/Inventory/Inventory.Domain.Tests   # green
```

### Step 5 — Application

- [ ] One aggregate per transaction (TXN-002)
- [ ] Ports declared here, implemented in Infrastructure (ARC-006)
- [ ] Integration events **enqueued to the outbox**, never published directly (REL-001, REL-002)
- [ ] Server-side validation at this boundary (SEC-031)
- [ ] Authorization on role **and** resource (SEC-021, SEC-022)
- [ ] Commands return an identifier + result, not domain data (ARC-030)

### Step 6 — Infrastructure

- [ ] EF configuration in `Configurations/`, not on entities (ARC-005)
- [ ] Migration created; **no cross-context foreign keys** (ARC-022)
- [ ] Outbox row written in the business transaction (REL-002)
- [ ] Relay claims with `FOR UPDATE SKIP LOCKED` (REL-005)
- [ ] Consumers use the inbox inside the business transaction (REL-021, REL-022)
- [ ] Consumers are tolerant readers (EVG-040, EVG-041, EVG-042)
- [ ] External DTOs mapped at the boundary; they do not reach Domain (ARC-014)
- [ ] Retry + circuit breaker + timeout on every remote call (RES-010, RES-020, RES-024)
- [ ] DLQ configured (RES-030)

```bash
docker compose up -d postgres rabbitmq redis
dotnet test tests/Services/Inventory/Inventory.Infrastructure.Tests   # Testcontainers (QAG-008)
```

### Step 7 — API

- [ ] Versioned path `/api/v1/...`, TLS only (COM-010)
- [ ] Error envelope with `correlationId` (COM-012); stable `code` values (COM-013)
- [ ] Pagination on every collection (COM-014)
- [ ] `Idempotency-Key` enforced on money/order writes (EDG-020)
- [ ] gRPC deadlines set by callers (COM-022); no cross-context writes (COM-024)
- [ ] Health checks exposed (OBS-033)

### Step 8 — Observability

- [ ] `Correlation-ID` survives every hop **including the broker** (OBS-002, OBS-011)
- [ ] `causationId` set on emitted messages (OBS-003)
- [ ] Structured logs carry the required fields; no PII or secrets (OBS-021, OBS-022)
- [ ] Metrics from spec § 11.1 emitted (OBS-030)
- [ ] Alerts configured, each linked to a runbook (OBS-031, OBS-032)

**Acceptance test for this step (OBS-034)** — run the feature locally, take the
`Correlation-ID` from the response, and reconstruct the entire flow in Jaeger and in the logs
**without reading code**. If you cannot, this step is not done.

### Step 9 — Pull Request

Work the **constitution's Appendix B** checklist. Reviewers block by citing rule IDs.

```markdown
## Spec
specs/007-stock-reservation/spec.md

## Constitution Check
- EDG-003: Strategy A (atomic conditional update) — see InventoryRepository.cs:84
- REL-002: outbox enqueued in the same transaction — see ReserveStockHandler.cs:41
- QAG-006: concurrency test — StockConcurrencyTests.cs:23

## Deviations
None.   ← or: "RES-011 retry budget raised to 8 — waiver W-003, expires 2026-12-01"
```

### Step 10 — Rollout

- [ ] Feature flag for risky changes (QAG-011)
- [ ] Blue-green or canary for checkout/payment paths (QAG-011)
- [ ] Dual-publish window running if an event version changed (EVG-035, EVG-036)
- [ ] Monitor the metrics from § 11.1 before removing the flag

---

## 5. Recipes

### 5.1 Add a Command

```
1. Application/<Aggregate>/Commands/<Name>/          Command, Handler, Validator, Result
2. Domain method on the aggregate                    invariants live here (ARC-031)
3. Api/Endpoints/                                    route + auth + idempotency filter
4. Tests: domain invariant, handler, endpoint, idempotency (QAG-005)
```

### 5.2 Publish a New Integration Event

```
1. contracts/events/<context>/<agg>.<verb>.v1.json   schema + golden samples (EVG-020, EVG-053)
2. event-governance.md § 7                           catalog row (EVG-054)
3. Domain/<Agg>/Events/                              domain event
4. Infrastructure/Messaging/Translators/             domain → integration (COM-032)
5. Verify the outbox row is written in the business transaction (REL-002)
6. Contract test: emitted message validates against the schema (EVG-052)
```

⚠️ Never publish a domain event straight to the broker (COM-032).

### 5.3 Consume an Event

```
1. Infrastructure/Messaging/Consumers/<Event>Consumer.cs
2. Claim via the inbox INSIDE the business transaction (REL-021, REL-022)
3. Tolerant reader configuration (EVG-040, EVG-041)
4. Unknown enum values handled without throwing (EVG-042)
5. Stale-event guard if ordering matters (REL-027)
6. Retry policy + DLQ (RES-011, RES-030)
7. Tests: happy path, duplicate delivery (QAG-005), out-of-order, poison → DLQ
```

### 5.4 Add a Saga Step

```
1. Update the spec's § 8 table FIRST — including the compensating action (SAG-002, SAG-025)
2. Application/Sagas/<Process>/                       state machine + persisted state
3. Confirm non-compensable steps remain last (SAG-029)
4. Deadline and per-step timeout set (SAG-030)
5. Tests: happy path + the NEW compensation branch (QAG-004)
```

### 5.5 Add a Custom Rule to the Constitution

```
1. constitution.md § XI.3, using the § XI.2 template
2. Allocate an ID from the reserved 900–999 range (EXT-001, EXT-005)
3. Name the enforcement mechanism — test, CI check, or lint (EXT-003)
4. Register it in Appendix A; bump MINOR (EXT-004)
```

---

## 6. Local Development

```bash
docker compose up -d                  # postgres, rabbitmq, redis, jaeger
dotnet tool restore
dotnet ef database update --project src/Services/Ordering/Ordering.Infrastructure
dotnet run --project src/Services/Ordering/Ordering.Api

dotnet test                           # everything
./scripts/check-coverage.sh           # QAG-002 gate
```

| Service | Local endpoint |
|---|---|
| PostgreSQL | `localhost:5432` |
| RabbitMQ management | `localhost:15672` |
| Redis | `localhost:6379` |
| Jaeger UI | `localhost:16686` — verify OBS-034 here |

**Migrations** — one `DbContext` per context, one migration history table per schema (ARC-021):

```bash
dotnet ef migrations add AddStockReservations \
  --project src/Services/Inventory/Inventory.Infrastructure \
  --startup-project src/Services/Inventory/Inventory.Api
```

---

## 7. Definition of Done

A feature is done when **every** box is checked. Not "mostly done" — done.

**Spec & contracts**
- [ ] Spec approved; no unresolved placeholders (template § 18)
- [ ] Contracts committed; compatibility CI green (COM-042, EVG-050)
- [ ] Event catalog updated (EVG-054)

**Code**
- [ ] Architecture tests pass (ARC-007)
- [ ] No cross-context type, table, or FK reference (ARC-010, ARC-020, ARC-022)
- [ ] Outbox atomic; consumers idempotent (REL-002, REL-020)
- [ ] Retry, breaker, timeout, DLQ in place (RES-010, RES-020, RES-024, RES-030)
- [ ] Saga compensations implemented and ordered correctly (SAG-025, SAG-028, SAG-029)
- [ ] Idempotency keys enforced on money/order writes (EDG-020)

**Tests**
- [ ] Every acceptance criterion has a passing test (QAG-001)
- [ ] Coverage gate met — 90% money/inventory, 80% otherwise (QAG-002)
- [ ] Idempotency, concurrency, and compensation tests present (QAG-004, QAG-005, QAG-006)
- [ ] Integration tests run on real infrastructure (QAG-008)

**Operations**
- [ ] Full flow traceable from one `Correlation-ID` (OBS-034)
- [ ] Metrics and alerts live; each alert has a runbook (OBS-031, OBS-032)
- [ ] Rollout plan executed; flag removed after the monitoring window

**Governance**
- [ ] Constitution Appendix B checklist worked in the PR
- [ ] Any deviation carries a waiver with a hard expiry (EXT-010, EXT-011)

---

*Related: [`constitution.md`](./constitution.md) · [`contexts.md`](./contexts.md) ·
[`event-governance.md`](./event-governance.md) ·
[`templates/feature-spec-template.md`](./templates/feature-spec-template.md)*
