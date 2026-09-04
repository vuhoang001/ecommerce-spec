# Context Map

**Satisfies**: `STK-004 [withdrawn citation]` — module boundaries, schema ownership, and event ownership recorded and
kept current with the code.

## Modules

`MOD-005 [withdrawn citation]` fixes the set at four. Adding or removing one is an amendment under `the Governance amendment clause`.

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
| catalog → promotion | synchronous read | `promotion_pricing.proto` | `COM-001`, `COM-002`, `COM-003`, `PRM-001 [withdrawn citation]` |
| promotion → catalog | event | `promotion.discount.changed.v1` | `REL-003`, `REL-004`, `REL-005`, `COM-006`, `COM-007`, `COM-008` |

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

## Deployables

Constitution v2.2.0 makes container packaging the delivery mechanism for every deployable
(`DEP-001`) and admits a frontend to the stack.

| Deployable | Source | Image | Built by |
|---|---|---|---|
| Backend host | `src/Host/ECommerce.Host` | `src/Host/ECommerce.Host/Dockerfile` | the `backend-image` CI job |
| Frontend | not built | — | — |

`DEP-002` requires the two to be independently buildable. The backend job installs no .NET SDK
and runs no `dotnet` command on the runner, so it demonstrates that the image builds itself from
the backend source tree alone — it would still pass with the frontend absent, as it is today.

The four modules above all live inside the **backend** deployable. Module extraction (`ARC-001`)
and deployable separation (`DEP-002`) are different axes: extracting a module later means adding
a deployable, not reorganising the frontend.

## Extraction readiness

Every module is four assemblies — `Contracts`, `Domain`, `Application`, `Infrastructure` — so
`ARC-001` is checkable by assembly reference and extraction moves whole projects rather than
splitting them.
