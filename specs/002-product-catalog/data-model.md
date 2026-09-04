# Phase 1 Data Model: Product Catalog

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-04

Every table below lives in the `catalog` schema, owned by `CatalogDbContext` alone (DAT-001). No
foreign key leaves the schema (DAT-002). Monetary columns are `bigint` minor units (MON-001).

---

## Entities

### Product

The purchasable item. Aggregate root for this module.

| Attribute | Type | Notes |
|---|---|---|
| `id` | `uuid` | PK |
| `name` | `text` | Required, 1–200 chars |
| `name_normalized` | `text` | Generated: `lower(immutable_unaccent(name))`. GIN trigram index (research.md R3) |
| `description` | `text` | Optional |
| `price_minor` | `bigint` | Minor units, `>= 0` |
| `currency_code` | `char(3)` | ISO 4217 |
| `stock_quantity` | `integer` | `>= 0`. Read here, never written (spec Out of Scope) |
| `status` | `text` | `Draft` \| `Active` \| `Hidden` \| `Discontinued` |
| `created_at` | `timestamptz` | Drives the default newest-first ordering |

**Invariants** (unit-tested per TST-002):
- `price_minor >= 0` — a negative price is unrepresentable.
- `stock_quantity >= 0`.
- `name` is non-empty after trimming.
- `currency_code` is the same across a product's price and any discount applied to it.

**Visibility**: a global query filter restricts every read to `status = Active` (FR-001, research.md
R9). A direct fetch of a non-Active product returns nothing, which the endpoint renders as not found
without disclosing existence (FR-002).

**Indexes**: `(status, created_at DESC)` for listings; `(status, price_minor)` for the range filter;
GIN trigram on `name_normalized` for search.

---

### Category

| Attribute | Type | Notes |
|---|---|---|
| `id` | `uuid` | PK |
| `name` | `text` | Required, unique |
| `slug` | `text` | Unique, URL-safe |

---

### ProductCategory

The many-to-many join. A product may belong to zero or many categories, and an uncategorised product
stays reachable by search and direct link (spec Edge Cases).

| Attribute | Type | Notes |
|---|---|---|
| `product_id` | `uuid` | FK → `catalog.product` |
| `category_id` | `uuid` | FK → `catalog.category` |

PK is `(product_id, category_id)`, which is what makes FR-006 hold — a product cannot appear twice in
one category's listing. Index on `(category_id, product_id)` for the browse path.

---

### ProductImage

| Attribute | Type | Notes |
|---|---|---|
| `id` | `uuid` | PK |
| `product_id` | `uuid` | FK → `catalog.product` |
| `url` | `text` | Required |
| `position` | `integer` | Gallery order, unique per product |
| `is_primary` | `boolean` | Exactly one per product when any image exists |

A product with no images renders without a gallery rather than failing (spec Edge Cases), so
`is_primary` is enforced by a partial unique index rather than a NOT NULL on the product.

---

### DiscountProjection — the spec's "discount copy"

Catalog's own filterable copy of the active discount per product. `spec.md` calls this the **discount
copy** (FR-013 through FR-015, FR-031); `DiscountProjection` is the same thing under its
implementation name. Introduced by research.md R1
because FR-026 must filter on a price Promotion owns. Non-authoritative by construction.

| Attribute | Type | Notes |
|---|---|---|
| `product_id` | `uuid` | PK, FK → `catalog.product` |
| `discounted_price_minor` | `bigint` | `>= 0` (FR-016) |
| `currency_code` | `char(3)` | Must match the product's |
| `promotion_id` | `uuid` | Which promotion produced it — logged per OBS-001 |
| `occurred_at` | `timestamptz` | From the source event; the ordering key for REL-004 |
| `retrieved_at` | `timestamptz` | When Catalog stored it; drives the FR-015 15-minute expiry |

**Rules**:
- A row older than 15 minutes by `retrieved_at` is not displayed (FR-015) and is not used by the
  price filter — the product then matches on its original price alone (FR-027).
- The consumer applies an update only when the incoming `occurred_at` is newer than the stored one,
  which is what makes it safe under unordered delivery (REL-004).
- Never written by Promotion, never read by any other module (DAT-001, PRM-001).

**Index**: `(discounted_price_minor)` filtered to non-expired rows, for FR-026.

---

### InboxMessage

Deduplication for the one consumer this feature registers (REL-003).

| Attribute | Type | Notes |
|---|---|---|
| `message_id` | `uuid` | Part of PK |
| `consumer` | `text` | Part of PK |
| `received_at` | `timestamptz` | |

PK `(message_id, consumer)`. The row is inserted in the same transaction as the projection update, so
a replayed message produces exactly one effect.

---

### OutboxMessage

Created by the MassTransit EF Core outbox in the `catalog` schema. **No Catalog code writes to it in
this feature** — it exists so the relay and its tests are proven before the first publisher (plan.md
Complexity Tracking).

---

### Rate limit state — deliberately not a table

The token bucket per caller lives in each instance's memory, not in PostgreSQL. Counting requests in
the database would turn every read into a write, spend the p95 budget on bookkeeping, and give the
read-only path a write it does not otherwise have. The cost is that the limit is per instance and so
approximate (research.md R11). **No table is added for rate limiting, and DAT-001 is untouched.**

### Projection seeding under redundancy

FR-031's start-up seed is guarded by a PostgreSQL advisory lock rather than a state table, so exactly
one instance seeds when several start together. The seed itself is an idempotent upsert into
`DiscountProjection`, which makes a lost lock cost duplicated work rather than a wrong projection
(research.md R12).

## Relationships

```text
Category 1 ──< ProductCategory >── * Product
Product  1 ──< ProductImage
Product  1 ──0..1 DiscountProjection
```

## State Transitions

`Product.status`: `Draft → Active → Hidden → Active`, and `Active → Discontinued` as a terminal
state. **No transition is triggered by this feature** — the authoring path owns them (spec Out of
Scope). They are modelled here only so the visibility filter and its tests have the full set to
assert against.

## Price Range Matching (FR-026, FR-027, FR-028)

A product matches range `[min, max]` when either holds:

1. `price_minor BETWEEN min AND max`, or
2. a non-expired `DiscountProjection` exists and `discounted_price_minor BETWEEN min AND max`.

The query is a single left join with an `OR` over the two columns, returning each product once
(FR-026). When condition 2 alone matched, the response carries both prices so the customer can see
why the product appeared (FR-028). With no projection row, only condition 1 applies (FR-027).
