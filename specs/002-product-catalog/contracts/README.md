# Contracts: Product Catalog

| File | Owner | Direction | Constitution |
|---|---|---|---|
| [`promotion_pricing.proto`](./promotion_pricing.proto) | Promotion module | Catalog reads Promotion | COM-001, PRM-001, PRM-003, MON-001 |
| [`promotion_discount_changed.md`](./promotion_discount_changed.md) | Promotion module | Catalog consumes | REL-003/004/005, MSG-001, MSG-002, MSG-003 |
| [`catalog-storefront.openapi.yaml`](./catalog-storefront.openapi.yaml) | Catalog module | Storefront reads Catalog | FR-001..FR-037 |

## Ownership notes

- The proto and the event are **Promotion's** contracts. This feature adds them to
  `ECommerce.Promotion.Contracts` because Catalog must compile against them; the Promotion module
  body is a later feature, and a controllable fake serves the port in tests (research.md R10).
- The **port** `IPromotionPricingPort` is Catalog's, declared in `Catalog.Application` and
  implemented in `Catalog.Infrastructure` — consumer-owned, implemented outside the domain, which is
  exactly what COM-001's architecture test asserts.
- Transport today is an in-process adapter over these proto types, not a network call
  (research.md R5).

## Operational surface

`/health/live` and `/health/ready` are part of the contract, not incidental plumbing — the load
balancer's behaviour during a Promotion outage depends on readiness deliberately **not** checking
Promotion (research.md R13, SC-008). Changing that check changes the feature's availability
behaviour, so it belongs here where it can be reviewed.

Every storefront endpoint may answer `429` with `RATE_LIMIT_EXCEEDED` and a `Retry-After` header
(FR-035, SC-014). The limit is per instance and therefore approximate (research.md R11).
