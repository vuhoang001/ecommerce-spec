# Quickstart: Product Catalog

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-04

How to run the feature and prove it works end to end. Implementation lives in `tasks.md` and the
code; this file is the validation guide.

## Prerequisites

- .NET 8 SDK
- Docker, running — Testcontainers starts PostgreSQL 16 and RabbitMQ per test run
- PostgreSQL extensions `unaccent` and `pg_trgm`, created by the first migration (research.md R3)

## Run the host

```bash
docker compose up -d postgres rabbitmq
dotnet run --project src/Host/ECommerce.Host
```

The storefront read endpoints are described in
[`contracts/catalog-storefront.openapi.yaml`](./contracts/catalog-storefront.openapi.yaml).

## Run the gates

Each command maps to a constitution rule. All of them run in CI and block the merge (GATE-001).

```bash
# Architecture rules — one test class per rule identifier (research.md R8)
dotnet test tests/ECommerce.ArchitectureTests

# Cross-schema foreign key scan over generated migrations (DAT-002)
./scripts/check-migrations.sh

# Domain invariants and price-range matching
dotnet test tests/Catalog/ECommerce.Catalog.UnitTests

# Endpoints against real PostgreSQL
dotnet test tests/Catalog/ECommerce.Catalog.IntegrationTests

# Promotion port contract, including unreachable behaviour
dotnet test tests/Catalog/ECommerce.Catalog.ContractTests

# Relay concurrency and inbox replay
dotnet test tests/Shared/ECommerce.Shared.Messaging.Tests

# Rate limit shape, readiness semantics, single-seed under redundancy
dotnet test tests/ECommerce.Catalog.ResilienceTests
```

## Validation scenarios

Each scenario proves an acceptance criterion. Ordered by the user stories in the spec.

### 1. Browse a category (US1)

Seed a category with 30 Active products, one of them stock 0, one of them also in a second category.

- Request page 1 → 24 items, `totalCount` 30, `page` 1 (FR-007).
- The stock-0 product is present with `isOutOfStock: true` (FR-005).
- The two-category product appears once in each category's listing (FR-006).
- An empty category returns `emptyReason: NO_PRODUCTS_IN_CATEGORY` with status 200 (FR-008).

### 2. Product detail (US2)

- An Active product returns name, description, price, images, `stockQuantity`, and every category
  (FR-009).
- With Promotion returning `applied`, `price.current` is the discounted amount and `price.original`
  is the list price (FR-010).
- A Hidden and a Discontinued product each return 404 with `PRODUCT_NOT_FOUND` — identical to a
  product that never existed (FR-002).

### 3. Search (US3)

Seed a product named `Cà phê sữa đá`.

- `?q=ca phe` returns it; `?q=CÀ PHÊ` returns it (FR-017, both directions).
- A Hidden product whose name matches is absent (FR-018, SC-002).
- `?q=` returns 400 with `EMPTY_KEYWORD`, not the catalogue (FR-019).

### 4. Filter (US4)

Seed a product priced 250,000 discounted to 180,000, with a non-expired projection row.

- `?minPriceMinor=200000&maxPriceMinor=300000` returns it, matched on original price.
- `?minPriceMinor=150000&maxPriceMinor=200000` returns it with
  `matchedOnDiscountedPriceOnly: true` and both prices shown (FR-026, FR-028).
- `?minPriceMinor=200000&maxPriceMinor=50000` returns 400 `MIN_EXCEEDS_MAX` — **not** an empty list
  (FR-022, FR-029).
- `?minPriceMinor=-1` returns 400 `NEGATIVE_PRICE_BOUND` (FR-025).
- Combining `categoryId` with a range returns only products satisfying both (FR-021).

### 5. Promotion degradation (FR-012, FR-013, FR-015, SC-008)

Point the port at the controllable fake (research.md R10).

- Fake returns `rejected` → undiscounted price shown, reason absent from the response, reason
  present in the log (FR-012, SC-009).
- Fake returns `unavailable` with a projection row 5 minutes old → projected price shown with
  `isOutOfDate: true` (FR-013).
- Same, with the row aged past 15 minutes → undiscounted price with `isOutOfDate: true` (FR-015).
- Same, with no projection row at all → undiscounted price with `isOutOfDate: true`.
- Every page still renders in all four cases (SC-008).

### 6. Projection consumer (REL-003, REL-004, REL-005)

- Deliver `promotion.discount.changed.v1` twice with the same `message_id` → one projection row,
  one inbox row (REL-003).
- Deliver two messages for one product in reverse `occurred_at` order → the newer one wins
  (REL-004).
- Deliver a payload carrying an unknown field → consumed without error (REL-005).
- Deliver `outcome: Withdrawn` → the projection row is removed; the product then matches on its
  original price alone (FR-027).

### 7. Rate limiting (FR-035, FR-037, SC-014)

- Exceed the configured budget from one caller → `429` with `reasonCode: RATE_LIMIT_EXCEEDED`, a
  `Retry-After` header, and `retryAfterSeconds` in the body (FR-035).
- The response is never a short page or an empty list (SC-014, FR-029).
- Exceed the limit while requesting a Hidden product → still `429`, and no non-Active product is
  ever returned because a check was skipped under load (FR-037).
- Run the same test against two instances and record the observed aggregate limit — it is expected
  to be roughly twice the per-instance budget. **This test documents the imprecision rather than
  asserting exactness** (research.md R11).

### 8. Availability and recovery (FR-036, SC-015, SC-016)

Run against two instances behind the load balancer. Record timings; SC-016 is met by the recording,
not by configuration.

- Kill one instance mid-traffic → zero failed requests (FR-036).
- Kill every instance → measure time to restored service; must be under 15 minutes (SC-016).
- Fail the database over → measure time to restored service; must be under 15 minutes (SC-016).
- Start two instances simultaneously from cold → the discount projection is seeded exactly once, and
  the seed count in the log is 1, not 2 (research.md R12, FR-031).
- Make Promotion unreachable → `/health/ready` still returns 200 on every instance, no instance is
  removed from rotation, and every page still renders (research.md R13, SC-008). **This is the test
  that catches the readiness probe being written the conventional way.**

### 9. Money (TXN-006)

- The architecture test fails the build if `float`, `double`, or `decimal` appears in a money path.
- Every monetary value on the wire is `amountMinor` as an integer plus a currency code — check the
  OpenAPI schema and the proto agree (FR-030).
- Every amount is a whole number of dong, and `currencyCode` is always `VND` (FR-032, FR-033,
  SC-012).

## Expected outcome

All seven test projects green, the migration scan clean, the recovery drill recorded with timings,
and every scenario above matching its stated result. At that point the feature satisfies SC-001
through SC-016.
