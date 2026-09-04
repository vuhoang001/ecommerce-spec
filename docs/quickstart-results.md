# Quickstart Validation Results

**Run**: 2026-09-04 | **Feature**: [002-product-catalog](../specs/002-product-catalog/quickstart.md)

Every scenario in `quickstart.md` mapped to the test that proves it. All test projects green:
**159 passed, 0 failed**.

| Scenario | Covered by | Result |
|---|---|---|
| 1. Browse a category (FR-003, FR-005, FR-006, FR-007, FR-008) | `BrowseCategoryTests`, `OutOfStockListingTests`, `MultiCategoryListingTests`, `EmptyCategoryTests`, `ListingFieldsTests` | ✅ |
| 2. Product detail (FR-009, FR-010, FR-002) | `ProductDetailTests`, `ProductDetailVisibilityTests`, `PromotionRejectionTests` | ✅ |
| 3. Search (FR-017, FR-018, FR-019) | `SearchDiacriticTests`, `SearchVisibilityTests`, `SearchValidationTests` | ✅ |
| 4. Filter (FR-021..FR-028) | `FilterTests`, `PriceRangeMatchingTests` | ✅ |
| 5. Promotion degradation (FR-012, FR-013, FR-015, SC-008) | `PromotionRejectionTests`, `PromotionUnavailableTests`, `ProjectionStalenessTests` | ✅ |
| 6. Projection consumer (REL-003, REL-004, REL-005) | `InboxDeduplicationTests`, `OutOfOrderDeliveryTests`, `TolerantReaderTests`, `EnvelopeValidationTests` | ✅ |
| 7. Rate limiting (FR-035, FR-037, SC-014) | `RateLimitTests` | ✅ |
| 8. Availability and recovery (FR-036, SC-015, SC-016) | `HealthProbeTests`, `ProjectionSeedTests` | ⚠️ **partial** |
| 9. Money (MON-001, FR-030, SC-012) | `MoneyTests`, `Mon001IntegerMoneyTests`, `MoneyRoundTripTests` | ✅ |

## Test projects

| Project | Tests |
|---|---|
| `ECommerce.Catalog.IntegrationTests` | 64 |
| `ECommerce.Shared.Messaging.Tests` | 21 |
| `ECommerce.Catalog.UnitTests` | 22 |
| `ECommerce.ArchitectureTests` | 17 |
| `ECommerce.Catalog.ContractTests` | 16 |
| `ECommerce.Shared.Kernel.Tests` | 10 |
| `ECommerce.Catalog.ResilienceTests` | 9 |
| **Total** | **159** |

## Not verified

**Scenario 8 is partial.** What runs automatically: readiness stays 200 while Promotion is
unreachable, and two instances starting cold seed the discount copy exactly once. What does NOT
run: killing an instance mid-traffic, total-outage restart timing, and database failover — all
three need a deployed, load-balanced environment with managed PostgreSQL. `SC-015` and `SC-016`
are therefore **not yet evidenced** (see `runbooks/catalog-recovery-drill.md`).

**The load test is written, not run.** `tests/performance/catalog-load-test.js` needs a
catalogue seeded to 100,000 products and a k6 runner. `SC-003` and `SC-004` are unmeasured.
