---

description: "Task list for Product Catalog implementation"
---

# Tasks: Product Catalog

**Input**: Design documents from `/specs/002-product-catalog/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md — all present

**Tests**: **REQUIRED, not optional.** Constitution v3.0.0 QAG-001 is NON-NEGOTIABLE: tests come from
acceptance criteria, are written before the implementation, and MUST be observed to fail first. Every
phase below puts its tests ahead of its implementation for that reason, and a task pair is done only
when the test was seen red before it went green.

**Terminology**: the spec's **discount copy** is implemented as `DiscountProjection` in
`catalog.discount_projection`. The two names denote one thing; the spec keeps the plain-language name
because SPC-001 bars implementation names from `spec.md`.

**Organization**: Grouped by user story so each can be implemented, tested, and demonstrated
independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel — **a different file from every other `[P]` task**, no dependency on
  an incomplete task. Two tasks writing one file are never both `[P]`.
- **[Story]**: Which user story the task serves (US1–US4)
- Every task names the exact file it touches

## Path Conventions

Modular monolith per plan.md: modules under `src/Modules/<Module>/`, shared code under `src/Shared/`,
the single host under `src/Host/`, tests under `tests/`, review and operational records under `docs/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repository skeleton and the CI gates that will enforce the constitution from the first
commit onward.

- [X] T001 Create the solution and project skeleton in `ECommerce.sln` (plan.md Project Structure)
- [X] T002 [P] Add nullable-enabled, warnings-as-errors build settings in `Directory.Build.props` (plan.md Technical Context)
- [X] T003 [P] Add central package version management in `Directory.Packages.props` (plan.md Primary Dependencies)
- [X] T004 [P] Add PostgreSQL 16 and RabbitMQ services for local runs in `docker-compose.yml` (plan.md Storage, quickstart.md Prerequisites)
- [X] T005 [P] Add the CI workflow running build plus every test project in `.github/workflows/ci.yml` (GATE-001)
- [X] T006 [P] Add the cross-schema foreign key scanner over generated migrations in `scripts/check-migrations.sh` (DAT-002)
- [X] T007 Wire `scripts/check-migrations.sh` into the CI workflow as a blocking step in `.github/workflows/ci.yml` (GATE-001)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The architecture gates, the money type, the domain types every story needs, the module's
database, and the cross-cutting request behaviour.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

**Why the domain types live here**: `Product`, `Category`, `ProductStatus`, and `ProductImage` are
needed by all four stories, and the visibility filter in T036 cannot be written before `Product`
exists. Placing them in any one story would make this phase impossible to complete.

### Architecture gates (write first — these read assemblies, so they compile before the code exists)

- [X] T008 [P] Failing architecture test that a module references only another module's `.Contracts` in `tests/ECommerce.ArchitectureTests/Mod001ModuleReferencesTests.cs` (ARC-001)
- [X] T009 [P] Failing architecture test that `*.Contracts` declares no entity, EF type, or handler in `tests/ECommerce.ArchitectureTests/Mod002ContractsContentTests.cs` (ARC-002)
- [X] T010 [P] Failing architecture test that no business type lives in `Shared` in `tests/ECommerce.ArchitectureTests/Mod003SharedPrimitivesTests.cs` (ARC-003)
- [X] T011 [P] Failing architecture test that the module assembly set is exactly catalog, user, order, promotion in `tests/ECommerce.ArchitectureTests/Mod005ModuleSetTests.cs` (not a constitution rule — a plan-level statement)
- [X] T012 [P] Failing architecture test banning `float`, `double`, and `decimal` in any money path in `tests/ECommerce.ArchitectureTests/Mon001IntegerMoneyTests.cs` (TXN-006)
- [X] T013 [P] Failing architecture test banning `TransactionScope` and multi-resource enlistment in `tests/ECommerce.ArchitectureTests/Txn002NoDistributedTransactionTests.cs` (TXN-002)

### Money and shared primitives

- [X] T014 [P] Failing unit tests for `Money` — whole dong only, rejects fractional and mismatched currency, arithmetic stays integral — in `tests/Shared/ECommerce.Shared.Kernel.Tests/MoneyTests.cs` (TXN-006, FR-032, FR-033)
- [X] T015 Implement `Money` as a readonly record struct over `long` plus an ISO 4217 code in `src/Shared/ECommerce.Shared.Kernel/Money.cs` (TXN-006)
- [X] T016 [P] Implement `Result`, `IClock`, and `PagedResult` primitives in `src/Shared/ECommerce.Shared.Kernel/Primitives/` (ARC-003)

### Domain types shared by every story

- [X] T017 [P] Failing domain invariant tests for `Product` — non-empty name, non-negative price and stock — in `tests/Catalog/ECommerce.Catalog.UnitTests/ProductInvariantTests.cs` (QAG-002, QAG-004, QAG-005)
- [X] T018 Create the `Product` aggregate with its invariants in `src/Modules/Catalog/ECommerce.Catalog.Domain/Product.cs` (data-model.md)
- [X] T019 [P] Create `Category` in `src/Modules/Catalog/ECommerce.Catalog.Domain/Category.cs` (data-model.md)
- [X] T020 [P] Create `ProductStatus` with Draft, Active, Hidden, Discontinued in `src/Modules/Catalog/ECommerce.Catalog.Domain/ProductStatus.cs` (data-model.md)
- [X] T021 [P] Create `ProductImage` with gallery position and primary flag in `src/Modules/Catalog/ECommerce.Catalog.Domain/ProductImage.cs` (data-model.md)

### Catalog database

- [X] T022 Create `CatalogDbContext` with `HasDefaultSchema("catalog")` in `src/Modules/Catalog/ECommerce.Catalog.Infrastructure/CatalogDbContext.cs` (DAT-001)
- [X] T023 Add EF configurations for product, category, the join table, and images in `src/Modules/Catalog/ECommerce.Catalog.Infrastructure/Configurations/` (data-model.md; depends on T018-T021)
- [X] T024 Add the initial migration creating the `catalog` schema, the `immutable_unaccent` wrapper, the `unaccent` and `pg_trgm` extensions, and the four tables with their listing indexes in `src/Modules/Catalog/ECommerce.Catalog.Infrastructure/Migrations/` (research.md R3, data-model.md)
- [X] T025 [P] Failing integration test asserting `scripts/check-migrations.sh` rejects a cross-schema foreign key in `tests/Catalog/ECommerce.Catalog.IntegrationTests/MigrationGuardTests.cs` (DAT-002)
- [X] T026 Failing test that a `SaveChanges` interceptor rejects a transaction modifying more than one aggregate root in `tests/Catalog/ECommerce.Catalog.IntegrationTests/Txn001OneAggregatePerTransactionTests.cs` (TXN-001; needs T018 and T022 to compile)

### Cross-cutting request behaviour

- [X] T027 [P] Failing tests that every rejection carries a reason code and never an empty result in `tests/Catalog/ECommerce.Catalog.IntegrationTests/ProblemDetailsTests.cs` (FR-029, SC-005)
- [X] T028 Implement the problem-details shape with the reason codes from the OpenAPI contract in `src/Host/ECommerce.Host/Errors/CatalogProblemDetails.cs` (FR-029)
- [X] T029 [P] Failing tests that an over-limit caller gets 429 with a reason code and `Retry-After` in `tests/ECommerce.Catalog.ResilienceTests/RateLimitTests.cs` (FR-035, SC-014)
- [X] T030 Implement the per-caller token-bucket limiter, budget divided by instance count, in `src/Host/ECommerce.Host/RateLimiting/CatalogRateLimiter.cs` (FR-035, research.md R11)
- [X] T031 Register the limiter ahead of every handler so a refused caller never reaches a query in `src/Host/ECommerce.Host/Program.cs` (FR-037)
- [X] T032 [P] Failing test that readiness returns 200 while Promotion is unreachable in `tests/ECommerce.Catalog.ResilienceTests/HealthProbeTests.cs` (research.md R13, SC-008)
- [X] T033 Implement `/health/live` and `/health/ready` checking database and migrations only, never Promotion, in `src/Host/ECommerce.Host/Health/HealthEndpoints.cs` (FR-036, research.md R13)
- [X] T034 [P] Configure structured logging with correlation identifiers in `src/Host/ECommerce.Host/Logging/LoggingSetup.cs` (OBS-001)
- [X] T035 [P] Failing test that a Hidden and a Discontinued product are absent from every read path in `tests/Catalog/ECommerce.Catalog.IntegrationTests/VisibilityFilterTests.cs` (FR-001, FR-002, SC-002)
- [X] T036 Implement the global query filter restricting every product read to Active in `src/Modules/Catalog/ECommerce.Catalog.Infrastructure/CatalogDbContext.cs` (FR-001, research.md R9; depends on T018)

**Checkpoint**: Money is integral, the domain types exist, the schema is migrated, the architecture
gates run red-to-green, every request is rate limited, and no non-Active product can escape. This
phase compiles on its own. User stories may begin.

---

## Phase 3: User Story 1 - Browse products by category (Priority: P1) 🎯 MVP

**Goal**: A customer opens a category and pages through the Active products it contains.

**Independent Test**: Seed a category with 30 Active products, one at stock 0 and one also belonging
to a second category. Page through it and confirm the count, the position, the "Out of stock" label,
and that the two-category product appears exactly once per listing.

### Tests for User Story 1 (write first, observe failing — QAG-001)

- [X] T037 [P] [US1] Failing integration test for paging a 30-product category at page size 24, asserting total and position, in `tests/Catalog/ECommerce.Catalog.IntegrationTests/BrowseCategoryTests.cs` (US1/AC1, FR-003, FR-007)
- [X] T038 [P] [US1] Failing integration test that a stock-0 product stays listed and labelled in `tests/Catalog/ECommerce.Catalog.IntegrationTests/OutOfStockListingTests.cs` (US1/AC2, FR-005, SC-007)
- [X] T039 [P] [US1] Failing integration test that a two-category product appears once per listing in `tests/Catalog/ECommerce.Catalog.IntegrationTests/MultiCategoryListingTests.cs` (US1/AC3, FR-006)
- [X] T040 [P] [US1] Failing integration test that an empty category returns 200 with `NO_PRODUCTS_IN_CATEGORY` in `tests/Catalog/ECommerce.Catalog.IntegrationTests/EmptyCategoryTests.cs` (US1/AC4, FR-008)
- [X] T041 [P] [US1] Failing integration test that each listed product carries its name, primary image, and current price in `tests/Catalog/ECommerce.Catalog.IntegrationTests/ListingFieldsTests.cs` (FR-004)

### Implementation for User Story 1

- [X] T042 [US1] Implement `BrowseCategoryQuery` returning a page plus total in `src/Modules/Catalog/ECommerce.Catalog.Application/Browse/BrowseCategoryQuery.cs` (FR-003, FR-007)
- [X] T043 [US1] Implement the category listing endpoint per the OpenAPI contract in `src/Host/ECommerce.Host/Endpoints/CategoryProductsEndpoint.cs` (FR-003, FR-004, FR-005, FR-006, FR-007)
- [X] T044 [US1] Map the empty-page reasons `NO_PRODUCTS_IN_CATEGORY` and `PAGE_BEYOND_LAST` in `src/Host/ECommerce.Host/Endpoints/CategoryProductsEndpoint.cs` (FR-008)

**Checkpoint**: A customer can browse any category, page through it, and see out-of-stock products
without any other story existing. **This is the MVP.**

---

## Phase 4: User Story 2 - View product detail (Priority: P1)

**Goal**: A customer opens one product and sees everything needed to decide, including a discounted
price when Promotion supplies one — and a sensible price when Promotion is down.

**Independent Test**: Open a product with images, description, stock, several categories, and an
active discount. Confirm every element renders and the two prices are distinguishable. Then make
Promotion fail and confirm the page still renders.

**This phase is split at 4A/4B** because it is the largest in the feature. 4A delivers a working
detail view on its own; 4B adds everything discount-related, and is what User Story 4 depends on.

### Phase 4A — Detail view at list price

- [X] T045 [P] [US2] Failing integration test that detail shows name, description, price, gallery, stock, and every category in `tests/Catalog/ECommerce.Catalog.IntegrationTests/ProductDetailTests.cs` (US2/AC1, FR-009)
- [X] T046 [P] [US2] Failing integration test that Hidden and Discontinued products return 404 identically to non-existent ones in `tests/Catalog/ECommerce.Catalog.IntegrationTests/ProductDetailVisibilityTests.cs` (US2/AC4, FR-002)
- [X] T047 [US2] Implement `GetProductDetailQuery` returning the product at its list price in `src/Modules/Catalog/ECommerce.Catalog.Application/Detail/GetProductDetailQuery.cs` (FR-009)
- [X] T048 [US2] Implement the product detail endpoint per the OpenAPI contract in `src/Host/ECommerce.Host/Endpoints/ProductDetailEndpoint.cs` (FR-009, FR-002)

**Checkpoint 4A**: a customer can open any product and see everything about it at its list price.
Demonstrable on its own, before any discount work exists.

### Phase 4B — Discount integration (tests first, observe failing — QAG-001)

- [X] T049 [P] [US2] Failing integration test that an applied discount shows both prices and matches exactly what the port returned in `tests/Catalog/ECommerce.Catalog.IntegrationTests/DiscountedDetailTests.cs` (US2/AC2, FR-010, SC-006)
- [X] T050 [P] [US2] Failing contract test that a promotion rejection shows the undiscounted price and logs the reason unshown in `tests/Catalog/ECommerce.Catalog.ContractTests/PromotionRejectionTests.cs` (FR-012, SC-009, PRM-003 [withdrawn citation])
- [X] T051 [P] [US2] Failing contract test that an unreachable Promotion falls back to the discount copy marked out of date in `tests/Catalog/ECommerce.Catalog.ContractTests/PromotionUnavailableTests.cs` (FR-013, SC-008, PRM-003 [withdrawn citation])
- [X] T052 [P] [US2] Failing contract test that a copy past 15 minutes, and an absent one, both fall back to the undiscounted price marked out of date in `tests/Catalog/ECommerce.Catalog.ContractTests/ProjectionStalenessTests.cs` (FR-015)
- [X] T053 [P] [US2] Failing contract test that calling the discount port twice with the same input yields the same result and changes no state in `tests/Catalog/ECommerce.Catalog.ContractTests/PromotionPortPurityTests.cs` (PRM-001 [withdrawn citation], FR-011)
- [X] T054 [P] [US2] Failing test that a replayed `promotion.discount.changed.v1` produces one effect in `tests/Shared/ECommerce.Shared.Messaging.Tests/InboxDeduplicationTests.cs` (REL-003)
- [X] T055 [P] [US2] Failing test that reverse-order delivery converges to the same discount copy state in `tests/Shared/ECommerce.Shared.Messaging.Tests/OutOfOrderDeliveryTests.cs` (REL-004)
- [X] T056 [P] [US2] Failing test that a payload with an unknown field is consumed without error in `tests/Catalog/ECommerce.Catalog.ContractTests/TolerantReaderTests.cs` (REL-005)
- [X] T057 [P] [US2] Failing test that a message missing any of `message_id`, `type`, `version`, `occurred_at`, `correlation_id`, or `causation_id` is rejected at the transport boundary in `tests/Shared/ECommerce.Shared.Messaging.Tests/EnvelopeValidationTests.cs` (COM-006)
- [X] T058 [P] [US2] Failing test that two instances starting cold seed the discount copy exactly once in `tests/ECommerce.Catalog.ResilienceTests/ProjectionSeedTests.cs` (FR-031, research.md R12)
- [X] T059 [P] [US2] Failing test that a discount starting, changing, or ending reaches the discount copy within 1 minute in `tests/Catalog/ECommerce.Catalog.IntegrationTests/DiscountPropagationTests.cs` (SC-011, FR-031)

- [X] T060 [P] [US2] Add `promotion_pricing.proto` and generation settings in `src/Modules/Promotion/ECommerce.Promotion.Contracts/` (contracts/promotion_pricing.proto, ARC-002)
- [X] T061 [US2] Declare the consumer-owned, read-only `IPromotionPricingPort` with no write method in `src/Modules/Catalog/ECommerce.Catalog.Application/Ports/IPromotionPricingPort.cs` (COM-001, PRM-001 [withdrawn citation])
- [X] T062 [US2] Implement the in-process adapter over the proto types in `src/Modules/Catalog/ECommerce.Catalog.Infrastructure/Promotion/InProcessPromotionPricingAdapter.cs` (COM-001, research.md R5)
- [X] T063 [US2] Add the port review checklist recording call depth and transaction isolation for every port implementation in `docs/reviews/port-review-checklist.md` (COM-002, COM-003, GATE-001)
- [X] T064 [P] [US2] Add the architecture test that the port is declared in the consumer and implemented outside its domain in `tests/ECommerce.ArchitectureTests/Com001PortOwnershipTests.cs` (COM-001)
- [X] T065 [P] [US2] Add the architecture test that no module references another's `Application` assembly in `tests/ECommerce.ArchitectureTests/Com004NoCrossModuleWriteTests.cs` (COM-004)
- [X] T066 [P] [US2] Add the architecture test that Catalog declares no discount calculation and consumes no Promotion message as a write in `tests/ECommerce.ArchitectureTests/Prm001NoDiscountCalculationTests.cs` (PRM-001 [withdrawn citation], FR-011)
- [X] T067 [US2] Create the `DiscountProjection` entity — the spec's discount copy — and its configuration in `src/Modules/Catalog/ECommerce.Catalog.Domain/DiscountProjection.cs` (FR-014, data-model.md)
- [X] T068 [US2] Add the migration creating `discount_projection` and its filtered price index in `src/Modules/Catalog/ECommerce.Catalog.Infrastructure/Migrations/` (FR-014, data-model.md)
- [X] T069 [US2] Configure MassTransit with the RabbitMQ transport and the EF Core inbox in `src/Shared/ECommerce.Shared.Messaging/MessagingSetup.cs` (REL-003)
- [X] T070 [US2] Implement the deduplicating consumer base inserting `(message_id, consumer)` in the business transaction in `src/Shared/ECommerce.Shared.Messaging/DeduplicatingConsumer.cs` (REL-003)
- [X] T071 [US2] Implement envelope validation at the transport boundary in `src/Shared/ECommerce.Shared.Messaging/EnvelopeValidator.cs` (COM-006)
- [X] T072 [US2] Implement the `promotion.discount.changed.v1` consumer maintaining the discount copy, applying only newer `occurred_at` in `src/Modules/Catalog/ECommerce.Catalog.Infrastructure/Consumers/DiscountChangedConsumer.cs` (REL-004, FR-031)
- [X] T073 [US2] Implement start-up seeding via `ListActiveDiscounts` guarded by a PostgreSQL advisory lock in `src/Modules/Catalog/ECommerce.Catalog.Infrastructure/Promotion/DiscountProjectionSeeder.cs` (FR-031, research.md R12)
- [X] T074 [US2] Extend `GetProductDetailQuery` to resolve price through live Promotion then the discount copy then undiscounted in `src/Modules/Catalog/ECommerce.Catalog.Application/Detail/GetProductDetailQuery.cs` (FR-010, FR-012, FR-013, FR-015, PRM-003 [withdrawn citation])
- [X] T075 [US2] Log every discount applied, rejected, and fallen back with product identifier and reason code in `src/Modules/Catalog/ECommerce.Catalog.Application/Detail/GetProductDetailQuery.cs` (OBS-001, SC-009)
- [X] T076 [US2] Enforce that a displayed discounted price is never below zero in `src/Modules/Catalog/ECommerce.Catalog.Application/Detail/GetProductDetailQuery.cs` (FR-016)

**Checkpoint 4B**: a customer always sees a price, whether Promotion is healthy, rejecting, or down.

---

## Phase 5: User Story 3 - Search by name (Priority: P2)

**Goal**: A customer types part of a product name and finds it regardless of letter case or
diacritics.

**Independent Test**: Seed `Cà phê sữa đá`, search `ca phe` and `CÀ PHÊ`, and confirm both return it
while a Hidden product with a matching name stays absent.

### Tests for User Story 3 (write first, observe failing — QAG-001)

- [X] T077 [P] [US3] Failing integration test that `ca phe` and `CÀ PHÊ` both match `Cà phê sữa đá` in `tests/Catalog/ECommerce.Catalog.IntegrationTests/SearchDiacriticTests.cs` (US3/AC1, FR-017)
- [X] T078 [P] [US3] Failing integration test that a Hidden product matching the keyword is absent in `tests/Catalog/ECommerce.Catalog.IntegrationTests/SearchVisibilityTests.cs` (US3/AC3, FR-018, SC-002)
- [X] T079 [P] [US3] Failing integration test that an empty keyword returns 400 `EMPTY_KEYWORD` rather than the catalogue in `tests/Catalog/ECommerce.Catalog.IntegrationTests/SearchValidationTests.cs` (US3/AC4, FR-019)

### Implementation for User Story 3

- [X] T080 [US3] Add the migration creating the `name_normalized` generated column and its GIN trigram index in `src/Modules/Catalog/ECommerce.Catalog.Infrastructure/Migrations/` (research.md R3, FR-017)
- [X] T081 [US3] Map `name_normalized` as a read-only generated column in `src/Modules/Catalog/ECommerce.Catalog.Infrastructure/Configurations/ProductConfiguration.cs` (data-model.md)
- [X] T082 [US3] Implement `SearchProductsQuery` normalising the keyword through the same function as the column in `src/Modules/Catalog/ECommerce.Catalog.Application/Search/SearchProductsQuery.cs` (FR-017)
- [X] T083 [US3] Implement the search endpoint with paging and keyword validation in `src/Host/ECommerce.Host/Endpoints/ProductSearchEndpoint.cs` (FR-019, FR-020)

**Checkpoint**: Search works in both diacritic directions and refuses an empty keyword with a reason.

---

## Phase 6: User Story 4 - Filter by category and price range (Priority: P2)

**Goal**: A customer narrows a listing by category, price band, or both — and a discounted product
appears when the price it would actually pay falls in the band.

**Independent Test**: Seed a product priced 250,000 discounted to 180,000. Confirm it appears for
200,000-300,000 on its original price, appears for 150,000-200,000 flagged as matched on the
discounted price with both prices shown, and that an inverted range returns an error rather than an
empty list.

**Depends on Phase 4B** for the discount copy (T067, T068, T072). Without it this story can satisfy
only FR-027 — matching on the original price alone.

### Tests for User Story 4 (write first, observe failing — QAG-001)

- [X] T084 [P] [US4] Failing unit tests for the price-range matching rule across both prices, including bounds exactly equal to min and to max in `tests/Catalog/ECommerce.Catalog.UnitTests/PriceRangeMatchingTests.cs` (FR-023, FR-026, FR-027, SC-010)
- [X] T085 [P] [US4] Failing integration test that category and range combine, returning only products satisfying both, in `tests/Catalog/ECommerce.Catalog.IntegrationTests/CombinedFilterTests.cs` (US4/AC1, FR-021)
- [X] T086 [P] [US4] Failing integration test that an inverted range returns 400 `MIN_EXCEEDS_MAX`, never an empty list, in `tests/Catalog/ECommerce.Catalog.IntegrationTests/FilterValidationTests.cs` (US4/AC2, FR-022, FR-029)
- [X] T087 [US4] Add cases to the same file for an omitted bound treated as unbounded and a negative bound rejected in `tests/Catalog/ECommerce.Catalog.IntegrationTests/FilterValidationTests.cs` (US4/AC3, US4/AC4, FR-024, FR-025; sequential after T086)
- [X] T088 [P] [US4] Failing integration test that a product matched only on its discounted price is flagged and shows both prices in `tests/Catalog/ECommerce.Catalog.IntegrationTests/DiscountedFilterMatchTests.cs` (FR-026, FR-028)
- [X] T089 [US4] Add a case to the same file that an expired discount copy row is excluded from the filter in `tests/Catalog/ECommerce.Catalog.IntegrationTests/DiscountedFilterMatchTests.cs` (FR-015, FR-027; sequential after T088)

### Implementation for User Story 4

- [X] T090 [US4] Implement `FilterProductsQuery` as a single left join matching on either price and returning each product once in `src/Modules/Catalog/ECommerce.Catalog.Application/Filter/FilterProductsQuery.cs` (FR-023, FR-026, data-model.md)
- [X] T091 [US4] Implement range validation producing `MIN_EXCEEDS_MAX` and `NEGATIVE_PRICE_BOUND` in `src/Modules/Catalog/ECommerce.Catalog.Application/Filter/PriceRangeValidator.cs` (FR-022, FR-025)
- [X] T092 [US4] Implement the filter endpoint setting `matchedOnDiscountedPriceOnly` per the OpenAPI contract in `src/Host/ECommerce.Host/Endpoints/ProductFilterEndpoint.cs` (FR-021, FR-028)

**Checkpoint**: All four user stories work independently, with User Story 4's discounted matching
resting on Phase 4B's discount copy.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: The infrastructure with no traffic yet, the whole-system guarantees that need every
endpoint to exist, and the evidence the success criteria demand.

### Outbox — built with zero publishers (plan.md Complexity Tracking)

- [X] T093 [P] Failing test that two concurrent relays publish each seeded outbox row exactly once in `tests/Shared/ECommerce.Shared.Messaging.Tests/RelayConcurrencyTests.cs` (REL-002)
- [X] T094 [P] Failing test capturing the delivery sweep SQL and asserting it contains `FOR UPDATE SKIP LOCKED` in `tests/Shared/ECommerce.Shared.Messaging.Tests/RelaySqlTests.cs` (REL-002, research.md R7)
- [X] T095 Configure the MassTransit EF Core outbox and relay in the `catalog` schema in `src/Shared/ECommerce.Shared.Messaging/OutboxSetup.cs` (REL-001)
- [X] T096 [P] Add the architecture test banning `IBus` and `IPublishEndpoint` outside the relay in `tests/ECommerce.ArchitectureTests/Rel001NoDirectPublishTests.cs` (REL-001)

### Whole-system guarantees (need every endpoint to exist)

- [X] T097 [P] Test that all four storefront endpoints answer without credentials and none reads a customer identity in `tests/Catalog/ECommerce.Catalog.IntegrationTests/AnonymousAccessTests.cs` (FR-034, SC-013)
- [X] T098 [P] Test that a price survives store then filter-compare then display as the identical integer, with no drift at any hop, in `tests/Catalog/ECommerce.Catalog.IntegrationTests/MoneyRoundTripTests.cs` (FR-030, SC-012)
- [X] T099 [P] Test that every Active product is returned by some category listing or search, and is then openable — at most two catalogue requests, none requiring its identifier in advance — in `tests/Catalog/ECommerce.Catalog.IntegrationTests/ProductReachabilityTests.cs` (SC-001)

### Evidence the success criteria demand

- [X] T100 Add the load test measuring the 300 ms boundary p95 for listing, search, and detail at 100,000 products and 200 requests/second in `tests/performance/catalog-load-test.js` (SC-003, SC-004)
- [X] T101 Add the recovery drill runbook covering single-instance kill, full kill, and database failover with recorded timings in `docs/runbooks/catalog-recovery-drill.md` (SC-016, research.md R14)
- [ ] T102 Execute the recovery drill and record the measured timings in `docs/runbooks/catalog-recovery-drill.md` (SC-015, SC-016)
- [X] T103 Record the observed two-instance aggregate rate limit, documenting the imprecision rather than asserting exactness, in `tests/ECommerce.Catalog.ResilienceTests/RateLimitTests.cs` (research.md R11; sequential after T029)

### Final validation

- [X] T104 Add the CI event-schema compatibility check against the main branch in `.github/workflows/ci.yml` (COM-008, GATE-001; sequential after T007)
- [X] T105 [P] Document the module's boundaries, schema ownership, and event ownership in `docs/context-map.md` (not a constitution rule — a plan-level statement)
- [X] T106 Run every scenario in `specs/002-product-catalog/quickstart.md` and record the results
- [X] T107 Verify every rule identifier cited in `specs/002-product-catalog/plan.md` has a passing named test in `tests/ECommerce.ArchitectureTests/` (GATE-001, GATE-001)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS every user story, and compiles standalone
- **User Story 1 (Phase 3)**: Depends on Foundational only
- **User Story 2 (Phase 4)**: Depends on Foundational only. 4A stands alone; 4B follows 4A
- **User Story 3 (Phase 5)**: Depends on Foundational only
- **User Story 4 (Phase 6)**: Depends on Foundational, **and on Phase 4B** for the discount copy
- **Polish (Phase 7)**: Depends on every story intended for the release. T097, T098, and T099 need
  all four endpoints to exist, which is why they sit here rather than in Foundational

### User Story Dependencies

- **US1 (P1)**: Independent. The MVP.
- **US2 (P1)**: Independent of US1. 4B builds the Promotion port and discount copy that US4 needs.
- **US3 (P2)**: Independent of US1, US2, and US4.
- **US4 (P2)**: **Not fully independent.** FR-026 requires the discount copy built in Phase 4B. Ship
  US4 before 4B and it satisfies FR-027 only — original-price matching — which fails SC-010 for any
  discounted product. This is the one real inter-story dependency in the feature.

### Within Each User Story

- Tests are written and observed failing before implementation (QAG-001, NON-NEGOTIABLE)
- Domain types before EF configurations before migrations
- Queries before endpoints
- Story complete and independently demonstrable before the next priority

### Parallel Opportunities

- T002-T006 in Setup run together
- T008-T013 (architecture gates) run together, and alongside T014 and T017
- T019-T021 (sibling domain types) run together once T018 lands
- Every `[P]` task writes a file no other `[P]` task writes — verified, so a parallel batch never
  collides. Tasks appending to a file another task created (T087, T089, T103, T104) are sequential
  and carry no `[P]`
- US1, US2, and US3 can be staffed in parallel once Foundational completes; US4 waits on Phase 4B

---

## Parallel Example: Phase 2 Foundational

```bash
# Write the six architecture gates together, observe them red (QAG-001):
Task: "ARC-001 test in tests/ECommerce.ArchitectureTests/Mod001ModuleReferencesTests.cs"
Task: "ARC-002 test in tests/ECommerce.ArchitectureTests/Mod002ContractsContentTests.cs"
Task: "ARC-003 test in tests/ECommerce.ArchitectureTests/Mod003SharedPrimitivesTests.cs"
Task: "MOD-005 [withdrawn citation] test in tests/ECommerce.ArchitectureTests/Mod005ModuleSetTests.cs"
Task: "TXN-006 test in tests/ECommerce.ArchitectureTests/Mon001IntegerMoneyTests.cs"
Task: "TXN-002 test in tests/ECommerce.ArchitectureTests/Txn002NoDistributedTransactionTests.cs"

# Then the three sibling domain types together, once Product lands:
Task: "Create Category in src/Modules/Catalog/ECommerce.Catalog.Domain/Category.cs"
Task: "Create ProductStatus in src/Modules/Catalog/ECommerce.Catalog.Domain/ProductStatus.cs"
Task: "Create ProductImage in src/Modules/Catalog/ECommerce.Catalog.Domain/ProductImage.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1: Setup — T001-T007
2. Phase 2: Foundational — T008-T036 (blocks everything; do not shortcut)
3. Phase 3: User Story 1 — T037-T044
4. **STOP and VALIDATE**: browse a category, page it, see out-of-stock products, get a reason for an
   empty one
5. Demo. A customer can find products; nothing else exists yet.

### Incremental Delivery

1. Setup + Foundational → money is integral, the domain exists, every request is limited, no hidden
   product can leak
2. + US1 → **MVP**: browse and page a category
3. + Phase 4A → open a product and see everything about it at list price
4. + Phase 4B → discounts shown, Promotion outage survived
5. + US3 → find a product by name in either diacritic direction
6. + US4 → filter by band, including on the price a customer would actually pay (needs 4B)
7. + Polish → outbox proven, anonymity, money round-trip and reachability verified, performance
   measured, recovery drilled and recorded

### What to cut under pressure

Phase 7's outbox tasks (T093-T096) are the only ones guarding no production traffic — Catalog
publishes nothing in this feature. Cutting them is the plan's own recorded recommendation. Nothing
else in this list is optional: every other task traces to a requirement or a constitution rule.

---

## Phase 8: Convergence

Appended by `/speckit-converge`. Each task traces to the artifact that requires it and the kind
of gap found. Nothing above this line was renumbered, reordered, or altered.

### Constitution violations (CRITICAL — resolve first)

- [X] T108 CRITICAL Configure an explicit dead-letter queue for every consumed queue and write the replay procedure in `docs/runbooks/catalog-messaging-replay.md`, then correct the claim in `src/Shared/ECommerce.Shared.Messaging/MessagingSetup.cs` that the procedure already exists per Constitution IV / REL-006 (missing)
- [X] T109 CRITICAL Apply pending migrations at start-up in `src/Host/ECommerce.Host/Program.cs`, guarded so concurrent instances cannot race, so a fresh database does not leave the container returning 503 and 500 until a CLI is run from outside the image per Constitution XI / DEP-001 (contradicts)

### Functional gaps

- [X] T110 Add a development seed script at `scripts/seed-dev-data.sh` that inserts the categories and products `quickstart.md` scenarios assume, so the documented run path can be followed by hand per quickstart.md (partial)
- [X] T111 Add a configurable CORS policy in `src/Host/ECommerce.Host/Program.cs`, allowing the storefront origin from configuration and defaulting to none, so a separate-deployable frontend can call the catalogue per Constitution X / UIX-001, UIX-002 (missing)
- [X] T112 [P] Add an integration test asserting the CORS policy allows the configured origin and rejects an unconfigured one in `tests/Catalog/ECommerce.Catalog.IntegrationTests/CorsPolicyTests.cs` per Constitution X / UIX-001 (missing)

### Contract and environment

- [X] T113 Emit the OpenAPI document from the host and add a CI step diffing it against `specs/002-product-catalog/contracts/catalog-storefront.openapi.yaml`, so a client can be generated from published server output rather than a hand-written file per Constitution X / UIX-002 (partial)
- [X] T114 [P] Declare a named volume for PostgreSQL in `docker-compose.yml` so a stale anonymous volume cannot leave `POSTGRES_USER` unapplied and produce `role "ecommerce" does not exist` per quickstart.md prerequisites (partial)
- [X] T115 [P] Remove the duplicated `REL-006` row from the Constitution Check table in `specs/002-product-catalog/plan.md` per plan.md Constitution Check (partial)

**Checkpoint**: T108 and T109 close the two constitution violations. T110, T111 and T112 are the
prerequisites a storefront feature depends on — without them a separate-origin frontend cannot
call the catalogue and no documented scenario can be run by hand.
