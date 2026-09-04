# Context Map

**Satisfies**: `STK-004` — module boundaries, schema ownership, and event ownership recorded and
kept current with the code.

## Modules

`MOD-005` fixes the set at four. Adding or removing one is an amendment under `GOV-002`.

| Module | Schema | Built | Owns |
|---|---|---|---|
| **catalog** | `catalog` | ✅ this feature | Product, Category, ProductImage, and the discount copy |
| **promotion** | `promotion` | contracts only | Discount calculation and the discount result |
| **user** | `user` | not built | — |
| **order** | `ordering` | not built | — |

## Schema ownership

`DAT-001`: each module owns exactly one schema and reads or writes no other module's tables.
`DAT-002`: no foreign key crosses a schema boundary — enforced by `scripts/check-migrations.sh`
in CI.

`catalog` holds: `product`, `category`, `product_category`, `product_image`,
`discount_projection`, `inbox_message`, `outbox_message`.

`discount_projection` deserves a note: it holds a **copy** of a Promotion fact, not a reference
into Promotion's tables. That copy exists because `FR-026` requires filtering on the discounted
price and `DAT-001` forbids reading Promotion's data directly (research.md R1).

## Cross-module communication

| From → To | Kind | Contract | Rules |
|---|---|---|---|
| catalog → promotion | synchronous read | `promotion_pricing.proto` | `COM-001`, `COM-002`, `COM-003`, `PRM-001` |
| promotion → catalog | event | `promotion.discount.changed.v1` | `REL-003`, `REL-004`, `REL-005`, `MSG-001`, `MSG-002`, `MSG-003` |

The port `IPromotionPricingPort` is declared in `ECommerce.Catalog.Application` — the **consumer**
— and implemented in `ECommerce.Catalog.Infrastructure`. Transport is an in-process adapter
today and a gRPC client after extraction; neither the port nor its callers change when that is
swapped (research.md R5).

## Event ownership

| Event | Publisher | Consumers |
|---|---|---|
| `promotion.discount.changed.v1` | promotion | catalog (`catalog.discount-projection`) |

**Catalog publishes nothing.** The outbox table and relay exist so `REL-001` and `REL-002` are
proven before the first publisher, not because this feature emits anything (plan.md Complexity
Tracking).

## Extraction readiness

Every module is four assemblies — `Contracts`, `Domain`, `Application`, `Infrastructure` — so
`MOD-001` is checkable by assembly reference and extraction moves whole projects rather than
splitting them.
