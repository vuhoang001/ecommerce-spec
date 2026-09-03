# Bounded Context Map

**Version**: 1.0.0 | **Last Amended**: 2026-09-03
**Governed by**: [`specs/constitution.md`](./constitution.md) v2.0.0

> This document defines the business boundaries of the platform. It is the authority on **who
> owns what**. Before writing any feature spec, locate the owning context here — a feature that
> does not fit cleanly into exactly one context is a signal that either the feature is
> under-analysed or this map needs amending (§ Amending This Map).
>
> Boundary rules are constitutional: **ARC-010** (exclusive domain ownership), **ARC-011**
> (contracts are the only coupling surface), **ARC-013** (duplicate concepts across contexts is
> correct), **ARC-020** (database-per-context).

---

## 1. Context Map Overview

```
                        ┌──────────────┐
                        │   IDENTITY   │  upstream to everything (authN/authZ)
                        └──────┬───────┘
                               │ (token claims, not calls)
                               ▼
   ┌────────────┐  gRPC   ┌──────────────┐  events   ┌──────────────┐
   │  CATALOG   │◄────────│   ORDERING   │──────────►│  INVENTORY   │
   │ (products, │  read   │  ★ SAGA      │  reserve  │ (stock,      │
   │  prices)   │────────►│  ORCHESTRATOR│◄──────────│ reservations)│
   └────────────┘ events  └───┬──────┬───┘  reserved └──────────────┘
        ▲                     │      │
        │ stock level         │      │ events
        │ (read model)        ▼      ▼
        │              ┌──────────┐ ┌──────────┐
        └──────────────│ PAYMENT  │ │ SHIPPING │
                       │ (auth,   │ │(shipments│
                       │ capture, │ │ carriers,│
                       │ refund)  │ │ tracking)│
                       └────┬─────┘ └────┬─────┘
                            │            │
                            └─────┬──────┘
                                  ▼  events (fan-in only)
                          ┌────────────────┐
                          │  NOTIFICATION  │  ← pure sink: nothing depends on it
                          └────────────────┘
```

### 1.1 Relationship Matrix

Read as **row → column**: "the row context depends on the column context in this way."

| ↓ from / → to | Identity | Catalog | Ordering | Inventory | Payment | Shipping | Notification |
|---|---|---|---|---|---|---|---|
| **Identity** | — | | | | | | ev |
| **Catalog** | | — | | ev | | | ev |
| **Ordering** | claims | gRPC + ev | ★ | cmd/ev | cmd/ev | cmd/ev | ev |
| **Inventory** | | ev | ev | — | | ev | ev |
| **Payment** | | | ev | | — | | ev |
| **Shipping** | | | ev | ev | | — | ev |
| **Notification** | ev | | ev | | ev | ev | — |

Legend — `gRPC` synchronous read (COM-020) · `cmd` command message via broker · `ev` integration
event subscription (COM-030) · `claims` JWT claims, no runtime call · `★` saga orchestrator
(SAG-012)

### 1.2 Strategic Classification

| Context | Type | Why | Investment |
|---|---|---|---|
| **Ordering** | **Core Domain** | The business *is* order fulfilment. Saga orchestration and order state are the competitive surface. | Highest — best engineers, deepest modelling |
| **Inventory** | **Core Domain** | Overselling is existential (EDG-001). Reservation semantics are genuinely hard. | Highest |
| **Payment** | **Core Domain** | Money correctness (EDG-020…EDG-028). Errors are unrecoverable and legally exposed. | Highest |
| **Catalog** | Supporting | Necessary, differentiating in merchandising, but well-understood. | Medium |
| **Shipping** | Supporting | Mostly integration with carriers; the domain logic is thin. | Medium |
| **Identity** | Generic | Solved problem. **SHOULD** favour proven libraries/providers over bespoke modelling. | Low — buy/adopt before build |
| **Notification** | Generic | Templating and delivery. No competitive value. | Low |

**Rule** — Generic contexts **SHOULD NOT** receive bespoke domain modelling effort. If a
discussion about Identity is consuming more design time than one about Inventory, the
prioritisation is wrong.

### 1.3 Integration Patterns Used

| Pattern | Where applied | Constitutional basis |
|---|---|---|
| **Customer/Supplier** | Ordering (customer) ← Inventory, Payment, Shipping (suppliers) | Ordering's needs drive their contracts |
| **Published Language** | `contracts/proto/`, `contracts/events/` | COM-040 |
| **Anti-Corruption Layer** | Every context boundary; especially Payment↔gateway, Shipping↔carrier | ARC-014 |
| **Conformist** | Payment → gateway API, Shipping → carrier API | We adapt to them; no leverage to negotiate |
| **Open Host Service** | Catalog gRPC, Inventory gRPC | One contract serves all consumers |
| **Shared Kernel** | ❌ **NOT USED** — deliberately | ARC-012 forbids shared business types |

---

## 2. Ubiquitous Language — Cross-Context Terms

These terms mean **different things in different contexts** (ARC-013). Using the wrong context's
meaning in a spec is a defect.

| Term | Identity | Catalog | Ordering | Inventory | Payment | Shipping |
|---|---|---|---|---|---|---|
| **Customer / User** | `UserAccount` — credentials, roles, verification state | — | `Customer` — a name, contact, addresses snapshotted onto the order | — | `Payer` — a gateway token holder | `Recipient` — a delivery address |
| **Product** | — | `Product` — content, media, categorisation, price | `OrderLine` — a *snapshot* of sku, name and price at purchase time | `StockItem` — a countable quantity at a location | — | `ShipmentItem` — weight and dimensions |
| **Price** | — | **Authoritative** list price | Snapshotted `UnitPrice` at placement; never re-read | — | `Amount` charged | — |
| **Quantity** | — | — | Ordered quantity | `on_hand` / `reserved` / `available` | — | Packed quantity |
| **Status** | Account status | Publication status | **Order lifecycle** | Reservation status | Payment status | Delivery status |

**Critical rule** — Ordering **MUST** snapshot product name and price onto `OrderLine` at
placement time. It **MUST NOT** re-read them from Catalog when displaying a historical order. A
price change in 2027 **MUST NOT** alter what a 2026 invoice says.

---

## 3. Context Definitions

Each context below states: purpose, explicit non-responsibilities, aggregates, commands, queries,
events published, events consumed, synchronous contracts, and owned data.

> **Reading the tables**: Commands are imperative and may be rejected. Queries are side-effect
> free (ARC-030). Events are past-tense facts (COM-034). Every published event name follows
> `<context>.<aggregate>.<past-tense-verb>.v<N>` (COM-033).

---

### 3.1 IDENTITY

**Purpose** — Establish and verify *who* an actor is, and what roles they hold.

**Owns** — accounts, credentials, sessions, roles, verification and lockout state.

**Explicitly NOT responsible for** —
- ❌ Customer addresses used for delivery (→ Ordering / Shipping)
- ❌ Customer purchase history or loyalty tier (→ Ordering)
- ❌ Payment methods or stored cards (→ Payment)
- ❌ Marketing preferences (→ Notification)

*Rationale*: Identity answers "who are you"; it is not the customer's profile drawer. Letting it
accumulate business attributes is the most common way a Generic context turns Core by accident.

#### Aggregate Roots

| Aggregate | Invariants enforced |
|---|---|
| `UserAccount` | Email unique and immutable once verified; password meets policy; lockout after N failed attempts (SEC-020); status transitions legal (`Pending → Active → Locked/Deactivated`) |
| `RefreshSession` | Bound to one account and one device fingerprint; single-use rotation; revocable individually or en masse |

**Value Objects** — `Email`, `PasswordHash`, `Role`, `PreferredLanguage`, `AccountStatus`

#### Commands

| Command | Notes |
|---|---|
| `RegisterAccount` | Idempotent on email (EDG-020) |
| `VerifyEmail` | Consumes a single-use token |
| `Authenticate` | Rate-limited; increments lockout counter in Redis (ARC-041) |
| `RefreshToken` | Rotates the refresh token; reuse detection revokes the session family |
| `ChangePassword` / `RequestPasswordReset` / `ResetPassword` | Reset token single-use, TTL-bounded |
| `AssignRole` / `RevokeRole` | Admin-only (SEC-021) |
| `LockAccount` / `UnlockAccount` / `DeactivateAccount` | |
| `RevokeSession` / `RevokeAllSessions` | |

#### Queries

`GetAccountById` · `GetAccountByEmail` *(internal only)* · `ListActiveSessions` · `GetRoles`

#### Events Published

| Event | Consumed by | Payload essentials |
|---|---|---|
| `identity.account.registered.v1` | Notification | accountId, email, preferredLanguage |
| `identity.account.email-verified.v1` | Notification | accountId, verifiedAt |
| `identity.account.password-reset-requested.v1` | Notification | accountId, resetToken *(SEC-012: short TTL, single use)* |
| `identity.account.locked.v1` | Notification | accountId, reason, lockedUntil |
| `identity.account.deactivated.v1` | Ordering, Notification | accountId, reason |
| `identity.account.role-assigned.v1` | — | accountId, role |

#### Events Consumed

*None.* Identity is deliberately upstream of everything and depends on nothing.

#### Synchronous Contracts

`contracts/proto/identity/v1/accounts.proto` — `GetAccount(accountId)`, `GetAccountsBatch(ids)`

⚠️ **Token validation MUST NOT be a gRPC call.** Services **MUST** validate JWTs locally against
the published signing key (JWKS). A per-request call to Identity would make it a synchronous
single point of failure for the entire platform, violating COM-023 and RES-020.

#### Data Owned — schema `identity`

`user_accounts` · `refresh_sessions` · `auth_audit_events` · `password_reset_tokens` ·
`outbox_messages`

---

### 3.2 CATALOG

**Purpose** — Describe what is for sale: content, structure, and list price.

**Owns** — products, variants, categories, brands, media, attributes, list pricing.

**Explicitly NOT responsible for** —
- ❌ How many units exist (→ Inventory)
- ❌ Whether an item can be purchased right now (→ Inventory availability)
- ❌ The price actually charged (→ Ordering snapshot, after promotions)
- ❌ Search ranking infrastructure (→ a future Search context; Catalog publishes events to it)

#### Aggregate Roots

| Aggregate | Invariants enforced |
|---|---|
| `Product` | SKU unique; at least one variant before publication; price > 0 with currency (TXN-011); cannot publish without required media and category |
| `Category` | Acyclic tree; a category with children cannot be deleted |
| `Brand` | Name unique |

**Entities** — `ProductVariant`, `ProductImage`, `AttributeValue`
**Value Objects** — `Sku`, `Money`, `Dimensions`, `Weight`, `SeoSlug`, `PublicationStatus`

#### Commands

`CreateProduct` · `UpdateProductContent` · `AddVariant` · `UpdateVariantPrice` ·
`PublishProduct` · `UnpublishProduct` · `AssignCategory` · `UploadMedia` · `CreateCategory` ·
`ReorderCategory` · `ArchiveProduct`

#### Queries

`GetProductBySlug` · `GetProductById` · `GetVariantsBySkus` · `ListByCategory` ·
`SearchProducts` · `GetPriceSnapshot`

> `SearchProducts` and `ListByCategory` **MUST** be served from a read model (ARC-032), never by
> loading Product aggregates. Both **MUST** paginate (COM-014).

#### Events Published

| Event | Consumed by | Payload essentials |
|---|---|---|
| `catalog.product.published.v1` | Inventory *(create stock record)*, Notification, Search | productId, skus[], name, categoryIds |
| `catalog.product.unpublished.v1` | Ordering *(block new lines)*, Search | productId, reason |
| `catalog.product.content-updated.v1` | Search | productId, changedFields[] |
| `catalog.variant.price-changed.v1` | Ordering *(cart re-validation, EDG-030)* | sku, oldPrice, newPrice, effectiveAt |
| `catalog.product.archived.v1` | Inventory, Search | productId |

#### Events Consumed

| Event | From | Reaction |
|---|---|---|
| `inventory.stock.level-changed.v1` | Inventory | Update the denormalized in-stock badge on the product read model (TXN-020: eventually consistent, target p99 < 5s) |

#### Synchronous Contracts

`contracts/proto/catalog/v1/products.proto` — `GetVariantsBySkus(skus[])` returning authoritative
name + price. **This is the checkout hot path** — Ordering calls it to snapshot line data
(COM-022: deadline required).

#### Data Owned — schema `catalog`

`products` · `product_variants` · `product_images` · `categories` · `brands` ·
`product_attributes` · `product_read_model` · `outbox_messages` · `inbox_messages`

---

### 3.3 ORDERING ★ *Core — Saga Orchestrator*

**Purpose** — Convert intent into a committed, fulfilled purchase. **Owns the order lifecycle and
orchestrates the placement Saga (SAG-040).**

**Owns** — carts, orders, order lines, order state machine, the placement saga.

**Explicitly NOT responsible for** —
- ❌ Whether stock exists (asks Inventory)
- ❌ Whether money moved (asks Payment)
- ❌ Where the parcel is (asks Shipping)
- ❌ Product content beyond the snapshot it took at placement

#### Aggregate Roots

| Aggregate | Invariants enforced |
|---|---|
| `Cart` | One active cart per customer; line quantity > 0; price re-validated at checkout (EDG-030); TTL-expired carts archived |
| `Order` | Immutable once `Confirmed` except via defined transitions; total = Σ lines + shipping + tax − discount, recomputed server-side (TXN-012); state machine enforced (EDG-032); at least one line |
| `OrderPlacementSaga` | State persisted every transition (SAG-020); optimistic concurrency on `row_version` (SAG-022); deadline enforced (SAG-030) |

**Entities** — `OrderLine`, `CartItem`
**Value Objects** — `OrderNumber`, `Money`, `ShippingAddress`, `BillingAddress`, `OrderStatus`,
`Quantity`

#### Order State Machine (EDG-032)

```
Draft ──► Pending ──► AwaitingPayment ──► Confirmed ──► Fulfilling ──► Shipped ──► Delivered
            │              │                  │              │             │
            ▼              ▼                  ▼              ▼             ▼
         Rejected      Cancelled          Cancelled     Cancelled     Returned
                            │                  │              │             │
                            └──────────────────┴──────────────┴─────────────┘
                                               ▼
                                            Refunded
```

Illegal transitions (`Cancelled → Shipped`, `Delivered → Pending`) **MUST** be rejected by the
`Order` aggregate itself, not merely prevented by the UI (EDG-032).

#### Commands

| Command | Notes |
|---|---|
| `CreateCart` / `AddCartItem` / `UpdateCartItemQuantity` / `RemoveCartItem` / `ClearCart` | |
| `Checkout` | Re-validates prices and availability (EDG-030) |
| `PlaceOrder` | **Requires `Idempotency-Key` (EDG-020, EDG-033)**; starts the saga |
| `CancelOrder` | Legal only pre-dispatch; triggers compensation (SAG-028) |
| `ConfirmOrder` | Saga-internal, on payment capture |
| `RequestReturn` / `ApproveReturn` | |

#### Queries

`GetOrderById` · `ListOrdersForCustomer` *(paginated, COM-014)* · `GetCart` ·
`GetOrderTimeline` *(saga + event history by `correlationId`, OBS-034)*

#### Events Published

| Event | Consumed by | Payload essentials |
|---|---|---|
| `ordering.order.placed.v1` | Inventory, Payment, Notification, Analytics | orderId, customerId, lines[], totals, currency, shippingAddress |
| `ordering.order.confirmed.v1` | Shipping, Notification | orderId, lines[], shippingAddress |
| `ordering.order.cancelled.v1` | Inventory, Payment, Shipping, Notification | orderId, reason, cancelledAt |
| `ordering.order.rejected.v1` | Notification | orderId, reason (`OutOfStock` \| `PaymentDeclined`) |
| `ordering.order.return-requested.v1` | Inventory, Payment | orderId, lines[], reason |

#### Commands Sent *(point-to-point, not events — COM-034)*

`inventory.reserve-stock` · `inventory.release-reservation` · `inventory.commit-reservation` ·
`inventory.restock` · `payment.authorize` · `payment.capture` · `payment.void-authorization` ·
`payment.refund` · `shipping.create-shipment`

#### Events Consumed

| Event | From | Saga reaction |
|---|---|---|
| `inventory.stock.reserved.v1` | Inventory | → authorize payment (SAG-040 step 3) |
| `inventory.stock.reservation-rejected.v1` | Inventory | → reject order, `OutOfStock` |
| `inventory.stock.committed.v1` | Inventory | → capture payment (step 5) |
| `payment.payment.authorized.v1` | Payment | → commit reservation (step 4) |
| `payment.payment.declined.v1` | Payment | → compensate: release reservation |
| `payment.payment.captured.v1` | Payment | → confirm order (step 6) |
| `payment.payment.capture-failed.v1` | Payment | → compensate: restock, void auth |
| `shipping.shipment.dispatched.v1` | Shipping | → status `Shipped` |
| `shipping.shipment.delivered.v1` | Shipping | → status `Delivered` |
| `catalog.variant.price-changed.v1` | Catalog | → flag affected carts for re-validation |
| `identity.account.deactivated.v1` | Identity | → cancel open orders per policy |

#### Synchronous Contracts

**Exposes** `contracts/proto/ordering/v1/orders.proto` — `GetOrderSummary(orderId)` for support
tooling.
**Consumes** Catalog `GetVariantsBySkus` at checkout (the only synchronous cross-context read on
the checkout path; COM-023 depth = 1).

#### Data Owned — schema `ordering`

`carts` · `cart_items` · `orders` · `order_lines` · `order_status_history` ·
`saga_order_placement` · `idempotency_keys` · `outbox_messages` · `inbox_messages`

---

### 3.4 INVENTORY ★ *Core*

**Purpose** — Know how many units exist, where, and guarantee they are never promised twice
(EDG-001).

**Owns** — stock levels, reservations, stock movements, warehouses, low-stock thresholds.

**Explicitly NOT responsible for** —
- ❌ Product names, descriptions, prices (→ Catalog; Inventory knows only SKUs)
- ❌ Deciding whether an order is allowed (→ Ordering; Inventory answers "can I hold N?")
- ❌ Physical logistics of dispatch (→ Shipping)

#### Aggregate Roots

| Aggregate | Invariants enforced |
|---|---|
| `StockItem` | `available = on_hand − reserved` always; `available >= 0` **never violated** (EDG-003 Strategy A); every mutation writes a ledger row (EDG-008) |
| `Reservation` | Belongs to exactly one order; carries `expires_at` (EDG-005); state machine `Held → Committed \| Released \| Expired`; release is idempotent (EDG-007) |
| `Warehouse` | Location codes unique |

**Value Objects** — `Sku`, `Quantity`, `WarehouseCode`, `ReservationStatus`, `MovementType`

#### The Availability Model (EDG-004)

```
on_hand    = physically present units
reserved   = units held by in-flight checkouts (each with expires_at)
available  = on_hand − reserved     ← what a new customer may take
```

Stock is **reserved** at saga step 2 and **committed** (physically deducted from `on_hand`) only
at step 4, after payment authorization succeeds (SAG-041).

#### Commands

| Command | Notes |
|---|---|
| `ReserveStock` | Atomic conditional update (EDG-003 Strategy A). Idempotent per `(orderId, sku)` |
| `ReleaseReservation` | **MUST be idempotent** (EDG-007) — this is a saga compensation path and will be retried |
| `CommitReservation` | Deducts `on_hand`; writes ledger |
| `Restock` | Compensation for a committed reservation (SAG-040 step 4) |
| `ReceiveStock` | Inbound goods receipt |
| `AdjustStock` | Manual correction — **MUST** record actor and reason (TXN-022) |
| `SetLowStockThreshold` | |
| `ExpireReservations` | Sweeper; idempotent and concurrency-safe (EDG-006) |

#### Queries

`GetAvailability(sku)` · `GetAvailabilityBatch(skus[])` · `GetStockLevel(sku, warehouse)` ·
`ListMovements(sku)` *(the audit ledger)* · `ListLowStock`

#### Events Published

| Event | Consumed by | Payload essentials |
|---|---|---|
| `inventory.stock.reserved.v1` | Ordering *(saga)* | orderId, reservationId, sku, quantity, expiresAt |
| `inventory.stock.reservation-rejected.v1` | Ordering *(saga)* | orderId, sku, requested, available, reason |
| `inventory.stock.committed.v1` | Ordering *(saga)*, Shipping | orderId, reservationId, sku, quantity |
| `inventory.stock.released.v1` | Ordering | orderId, reservationId, reason (`Compensation` \| `Expired` \| `Cancelled`) |
| `inventory.stock.level-changed.v1` | Catalog *(in-stock badge)* | sku, available, onHand |
| `inventory.stock.low-threshold-reached.v1` | Notification *(ops alert)* | sku, available, threshold |
| `inventory.stock.replenished.v1` | Catalog, Notification *(back-in-stock)* | sku, available |

#### Events Consumed

| Event | From | Reaction |
|---|---|---|
| `catalog.product.published.v1` | Catalog | Create `StockItem` rows for new SKUs at zero |
| `catalog.product.archived.v1` | Catalog | Mark SKU inactive; block new reservations |
| `ordering.order.cancelled.v1` | Ordering | Release or restock depending on saga stage |
| `ordering.order.return-requested.v1` | Ordering | Restock on return approval |
| `shipping.shipment.returned.v1` | Shipping | Restock returned units |

#### Synchronous Contracts

`contracts/proto/inventory/v1/availability.proto` — `CheckAvailability(sku, qty)`,
`CheckAvailabilityBatch(items[])`

⚠️ A gRPC availability check is a **hint for display only**. It **MUST NOT** be treated as a
reservation. Only the `ReserveStock` command holds stock. Code that checks availability
synchronously and then proceeds as though the stock is secured is exactly the EDG-002
read-then-write race.

#### Data Owned — schema `inventory`

`stock_items` · `reservations` · `stock_movements` *(append-only ledger)* · `warehouses` ·
`outbox_messages` · `inbox_messages`

---

### 3.5 PAYMENT ★ *Core*

**Purpose** — Move money correctly, exactly once, and prove it afterwards.

**Owns** — payments, authorizations, captures, refunds, gateway tokens, payment attempts,
reconciliation.

**Explicitly NOT responsible for** —
- ❌ Deciding what the order costs (→ Ordering computes; Payment charges what it is told)
- ❌ Storing card data — **forbidden outright** (SEC-001)
- ❌ Fraud scoring beyond gateway-provided signals (→ a future Risk context)

#### Aggregate Roots

| Aggregate | Invariants enforced |
|---|---|
| `Payment` | Amount > 0 with currency (TXN-011); state machine `Pending → Authorized → Captured \| Voided`; Σ refunds ≤ captured amount (EDG-028); every attempt persisted with tri-state outcome (EDG-025) |
| `Refund` | ≤ remaining capturable balance; idempotent per idempotency key |
| `PaymentMethod` | Holds a gateway **token only** — never a PAN (SEC-001, SEC-002) |

**Value Objects** — `Money`, `IdempotencyKey`, `GatewayReference`, `PaymentStatus`,
`CardFingerprint` *(brand + last4 + expiry only)*

#### The `Unknown` State (EDG-025)

```
                    ┌─────────► Succeeded
Attempt ── call ────┼─────────► Failed
                    └─ timeout ► Unknown ──► reconciliation job ──► Succeeded | Failed
```

`Unknown` is **first-class and mandatory**. Treating a gateway timeout as failure double-charges;
treating it as success ships unpaid goods. A retry after `Unknown` **MUST** reuse the same
deterministic gateway idempotency key (EDG-024).

#### Commands

| Command | Notes |
|---|---|
| `AuthorizePayment` | Reserves funds. Deterministic gateway key `{orderId}:{attempt}` (EDG-024) |
| `CapturePayment` | Settles authorized funds |
| `VoidAuthorization` | Saga compensation (SAG-040 step 3) — **MUST** be idempotent |
| `RefundPayment` | Saga compensation post-capture — **MUST** be idempotent (EDG-028) |
| `RegisterPaymentMethod` | Stores a gateway token |
| `HandleGatewayWebhook` | Signature verified before parsing (COM-016); idempotent (duplicates are certain) |
| `ReconcileSettlement` | Daily job (EDG-026) |

#### Queries

`GetPayment(paymentId)` · `ListPaymentsForOrder(orderId)` · `GetRefundableBalance(paymentId)` ·
`ListUnknownAttempts` *(reconciliation queue)*

#### Events Published

| Event | Consumed by | Payload essentials |
|---|---|---|
| `payment.payment.authorized.v1` | Ordering *(saga)* | orderId, paymentId, amount, currency, authorizedAt |
| `payment.payment.declined.v1` | Ordering *(saga)*, Notification | orderId, reason, gatewayCode |
| `payment.payment.captured.v1` | Ordering *(saga)*, Notification | orderId, paymentId, amount |
| `payment.payment.capture-failed.v1` | Ordering *(saga)* | orderId, paymentId, reason |
| `payment.payment.voided.v1` | Ordering | orderId, paymentId |
| `payment.refund.issued.v1` | Ordering, Notification | orderId, refundId, amount, reason |
| `payment.reconciliation.discrepancy-detected.v1` | Notification *(ops alert)* | paymentId, expected, actual |

> Payload rule: these events **MUST NOT** carry card data, tokens, or gateway credentials
> (SEC-012).

#### Events Consumed

| Event | From | Reaction |
|---|---|---|
| `ordering.order.placed.v1` | Ordering | Create a `Pending` payment record |
| `ordering.order.cancelled.v1` | Ordering | Void or refund per stage |
| `ordering.order.return-requested.v1` | Ordering | Prepare refund on approval |

#### Synchronous Contracts

`contracts/proto/payment/v1/payments.proto` — `GetPaymentStatus(orderId)` for support tooling
only. **Not on the checkout path** — the saga is driven by events (COM-030).

#### Data Owned — schema `payment`

`payments` · `payment_attempts` · `refunds` · `payment_methods` · `idempotency_keys` ·
`gateway_webhook_log` · `settlement_reconciliation` · `outbox_messages` · `inbox_messages`

---

### 3.6 SHIPPING

**Purpose** — Get physical goods to the recipient and report where they are.

**Owns** — shipments, packages, carrier assignment, tracking, delivery outcomes, returns
logistics.

**Explicitly NOT responsible for** —
- ❌ Deducting stock (→ Inventory)
- ❌ Refunding a failed delivery (→ Payment, driven by Ordering)
- ❌ The customer's account address book (→ Ordering snapshot)

#### Aggregate Roots

| Aggregate | Invariants enforced |
|---|---|
| `Shipment` | Belongs to one order; ≥ 1 package; state machine `Created → Assigned → Dispatched → InTransit → Delivered \| Failed \| Returned`; **once `Dispatched` it is non-compensable** (SAG-029) |
| `Carrier` | Code unique; service levels declared |
| `ReturnShipment` | References an original shipment |

**Value Objects** — `TrackingNumber`, `DeliveryAddress`, `Weight`, `Dimensions`,
`ServiceLevel`, `ShipmentStatus`

#### Commands

`CreateShipment` · `AssignCarrier` · `GenerateLabel` · `DispatchShipment` ·
`RecordTrackingUpdate` · `MarkDelivered` · `RecordDeliveryFailure` · `InitiateReturn` ·
`ReceiveReturn`

#### Queries

`GetShipment(shipmentId)` · `TrackByOrder(orderId)` · `ListPendingDispatch` ·
`GetCarrierRates(address, weight)`

#### Events Published

| Event | Consumed by | Payload essentials |
|---|---|---|
| `shipping.shipment.created.v1` | Ordering, Notification | orderId, shipmentId, items[] |
| `shipping.shipment.dispatched.v1` | Ordering, Notification | orderId, trackingNumber, carrier, estimatedDelivery |
| `shipping.shipment.in-transit.v1` | Notification | trackingNumber, location, timestamp |
| `shipping.shipment.delivered.v1` | Ordering, Notification | orderId, deliveredAt, signedBy |
| `shipping.shipment.delivery-failed.v1` | Ordering, Notification | orderId, reason, attemptNumber |
| `shipping.shipment.returned.v1` | Ordering, Inventory *(restock)*, Payment | orderId, items[], reason |

#### Events Consumed

| Event | From | Reaction |
|---|---|---|
| `ordering.order.confirmed.v1` | Ordering | Create shipment (SAG-040 step 7 — **non-compensable, ordered last**) |
| `ordering.order.cancelled.v1` | Ordering | Cancel shipment **only if not yet dispatched**; otherwise initiate return |
| `inventory.stock.committed.v1` | Inventory | Confirm pickable quantities |

#### Synchronous Contracts

`contracts/proto/shipping/v1/shipments.proto` — `GetShipmentStatus(orderId)`,
`EstimateDelivery(address, items)` *(used by Ordering at checkout for the delivery estimate;
COM-022 deadline required, RES-023 fallback: show a generic range if the breaker is open)*

**ACL required** (ARC-014) — carrier APIs (GHN, GHTK, Viettel Post, DHL) are Conformist
integrations. Carrier DTOs **MUST** be translated at the boundary and **MUST NOT** reach the
`Domain` layer.

#### Data Owned — schema `shipping`

`shipments` · `packages` · `shipment_items` · `carriers` · `tracking_events` ·
`return_shipments` · `outbox_messages` · `inbox_messages`

---

### 3.7 NOTIFICATION

**Purpose** — Deliver messages to humans across channels, honouring their preferences.

**Owns** — notification requests, templates, delivery attempts, channel preferences, opt-outs.

**Explicitly NOT responsible for** —
- ❌ Deciding *whether* a business event happened (it only reacts)
- ❌ Business data — it receives what it needs in the event payload (COM-036)

**Topology** — Notification is a **pure sink**. It consumes from every context and **nothing
depends on it**. This is deliberate: a notification failure **MUST NOT** fail a business
operation (RES-023).

#### Aggregate Roots

| Aggregate | Invariants enforced |
|---|---|
| `NotificationRequest` | Idempotent per `(recipientId, templateId, correlationId)` — **prevents duplicate emails on event redelivery (REL-020)**; respects opt-out; retry budget bounded (RES-011) |
| `NotificationTemplate` | Versioned; localized per `PreferredLanguage`; required variables declared |
| `ChannelPreference` | Per recipient per category; opt-out is absolute for marketing, ignored for transactional |

**Value Objects** — `Channel` *(Email \| Sms \| Push \| InApp)*, `TemplateId`, `Locale`,
`DeliveryStatus`

#### Commands

`SendNotification` · `UpdateChannelPreference` · `OptOut` · `RegisterDeviceToken` ·
`RetryFailedDelivery` · `PublishTemplate`

#### Queries

`GetNotificationHistory(recipientId)` · `GetPreferences(recipientId)` ·
`GetDeliveryStatus(notificationId)`

#### Events Published

| Event | Consumed by | Payload essentials |
|---|---|---|
| `notification.notification.sent.v1` | Analytics | notificationId, channel, templateId, sentAt |
| `notification.notification.failed.v1` | Ops alerting | notificationId, channel, reason, attempts |
| `notification.notification.bounced.v1` | Identity *(flag invalid email)* | recipientId, channel, reason |

#### Events Consumed *(fan-in from every context)*

| From | Events | Template |
|---|---|---|
| Identity | `account.registered`, `account.email-verified`, `account.password-reset-requested`, `account.locked` | Welcome, verification, reset, security alert |
| Ordering | `order.placed`, `order.confirmed`, `order.cancelled`, `order.rejected` | Order receipt, confirmation, cancellation |
| Payment | `payment.captured`, `payment.declined`, `refund.issued` | Payment receipt, failure, refund notice |
| Shipping | `shipment.dispatched`, `shipment.delivered`, `shipment.delivery-failed` | Dispatch + tracking, delivery, failure |
| Inventory | `stock.low-threshold-reached`, `stock.replenished` | Ops alert, back-in-stock |

#### Synchronous Contracts

*None exposed.* Notification is event-driven only.

**ACL required** — email/SMS/push providers (SendGrid, Twilio, FCM, Zalo ZNS) are Conformist
integrations behind circuit breakers (RES-020).

#### Data Owned — schema `notification`

`notification_requests` · `delivery_attempts` · `templates` · `channel_preferences` ·
`device_tokens` · `suppression_list` · `inbox_messages` · `outbox_messages`

---

## 4. Cross-Context Flow Reference

### 4.1 Order Placement — the canonical flow

Maps directly to **SAG-040**. Every hop carries one `correlationId` (OBS-001, SAG-021).

| # | Context | Trigger | Action | Emits |
|---|---|---|---|---|
| 1 | Ordering | `PlaceOrder` + `Idempotency-Key` | Snapshot prices from Catalog (gRPC); create `Order(Pending)`; start saga | `ordering.order.placed.v1` |
| 2 | Inventory | `inventory.reserve-stock` | Atomic conditional update (EDG-003 A) | `stock.reserved` \| `stock.reservation-rejected` |
| 3 | Payment | `payment.authorize` | Gateway auth, deterministic key (EDG-024) | `payment.authorized` \| `payment.declined` |
| 4 | Inventory | `inventory.commit-reservation` | Deduct `on_hand`; ledger row | `stock.committed` |
| 5 | Payment | `payment.capture` | Gateway capture | `payment.captured` \| `capture-failed` |
| 6 | Ordering | `payment.captured` | Order → `Confirmed` | `ordering.order.confirmed.v1` |
| 7 | Shipping | `order.confirmed` | Create shipment ⚠ **non-compensable (SAG-029)** | `shipment.created` |
| 8 | Notification | multiple | Send confirmation ⚠ non-compensable | `notification.sent` |

### 4.2 Compensation Paths

| Failure at | Compensations, in reverse order (SAG-028) | Customer sees |
|---|---|---|
| Step 2 | *(none needed)* | Rejected — out of stock |
| Step 3 | release reservation | Rejected — payment declined |
| Step 4 | void authorization → release reservation | Rejected — stock unavailable |
| Step 5 | restock → void authorization | Rejected — payment failed |
| After 6 | refund → restock | Cancelled + refund issued |
| After 7 | refund → return shipment → restock on receipt | Cancelled, return initiated |

### 4.3 Product Publication

`Catalog.PublishProduct` → `catalog.product.published.v1` → **Inventory** creates zero-quantity
`StockItem` rows · **Search** indexes · **Notification** alerts merchandising.

### 4.4 Back-in-Stock

`Inventory.ReceiveStock` → `inventory.stock.replenished.v1` → **Catalog** updates the in-stock
badge (eventually consistent, p99 < 5s per TXN-020) · **Notification** fans out to waitlisted
customers.

---

## 5. Anti-Patterns — Rejected by Construction

| ❌ Anti-pattern | Why it is forbidden | Rule |
|---|---|---|
| Ordering reads `catalog.products` directly by SQL | Cross-context table access | ARC-020 |
| A shared `Product` class referenced by Catalog and Ordering | Distributed monolith; neither can deploy alone | ARC-012 |
| Ordering calls `Inventory.DeductStock` over gRPC | Synchronous cross-context **write** | COM-024 |
| Inventory calls back to Ordering to ask "is this order still valid?" | Circular sync dependency; deadlock under load | COM-023 |
| Notification queries Ordering for the customer's email | Under-specified event; enrich the payload instead | COM-036 |
| A `FOREIGN KEY` from `ordering.orders` to `identity.user_accounts` | Cross-context referential integrity | ARC-022 |
| Order total recomputed from live Catalog prices when rendering an old invoice | History mutates retroactively | § 2 snapshot rule |
| gRPC availability check treated as a stock hold | The EDG-002 read-then-write race | EDG-002 |
| Saga state held in memory | Every in-flight order lost on restart | SAG-020 |
| Shipment dispatched before payment captured | Non-compensable step ordered too early | SAG-029 |

---

## 6. Future Contexts — Not Yet In Scope

Named here so they are **not** absorbed into existing contexts by accident (ARC-010).

| Context | Would own | Currently handled by | Extract when |
|---|---|---|---|
| **Pricing & Promotion** | Discounts, vouchers, campaigns, tiered pricing | List price in Catalog; discounts computed in Ordering | Promotion rules exceed simple percentage/fixed logic |
| **Search** | Indexing, ranking, facets, autocomplete | `SearchProducts` read model in Catalog | Query latency or ranking sophistication demands a dedicated engine |
| **Review & Rating** | Reviews, ratings, moderation | — | Feature is prioritised |
| **Risk & Fraud** | Scoring, velocity rules, manual review queue | Gateway signals in Payment | Chargeback rate justifies it |
| **Loyalty** | Points, tiers, redemption | — | Feature is prioritised |
| **Analytics** | Reporting, funnels, cohorts | — | Event volume justifies a dedicated consumer |

**ARC-024 applies**: extraction requires recorded evidence of an independent scaling or isolation
need, captured in an ADR *before* work begins.

---

## 7. Amending This Map

1. A new context, or a change of ownership between contexts, **MUST** be proposed as a PR editing
   this file, with an ADR recording the reasoning.
2. Moving a capability between contexts **MUST** include a data migration plan and a contract
   deprecation plan (`specs/event-governance.md` § Deprecation).
3. A feature that does not fit cleanly into one context **MUST** be resolved here **before** its
   spec is written. Writing the spec first and discovering the boundary later is how boundaries
   erode.
4. Adding a context is a **MINOR** amendment; moving ownership is **MAJOR**.

---

*Related: [`constitution.md`](./constitution.md) · [`event-governance.md`](./event-governance.md)
· [`guidelines.md`](./guidelines.md) · [`templates/feature-spec-template.md`](./templates/feature-spec-template.md)*
