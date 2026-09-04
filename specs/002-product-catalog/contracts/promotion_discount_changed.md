# Event Contract: `promotion.discount.changed.v1`

**Publisher**: Promotion module | **Consumer**: Catalog module (the one consumer this feature
registers) | **Transport**: RabbitMQ via MassTransit

Named per MSG-002's `<context>.<aggregate>.<past-tense-verb>.v<N>`. Consumed to maintain
`catalog.discount_projection` (research.md R1). Breaking changes require `.v2` alongside `.v1` until
Catalog has migrated (MSG-003).

## Envelope (MSG-001)

Every message carries `message_id`, `type`, `version`, `occurred_at`, `correlation_id`, and
`causation_id`. A message missing any of these is rejected at the transport boundary.

## Payload

| Field | Type | Notes |
|---|---|---|
| `product_id` | uuid | Which product's pricing changed |
| `promotion_id` | uuid | Which promotion caused it |
| `outcome` | enum | `Applied` \| `Withdrawn` |
| `discounted_price_minor` | int64 | Present when `Applied`; integer minor units (MON-001) |
| `currency_code` | string | ISO 4217 |

## Consumer obligations

- **REL-003**: insert `(message_id, "catalog.discount-projection")` into the inbox in the same
  transaction as the projection write. A replay produces one effect.
- **REL-004**: apply only when the envelope's `occurred_at` is newer than the stored `occurred_at`.
  Reverse-order delivery converges to the same projection state.
- **REL-005**: ignore unknown fields. A contract test delivers a payload with an added field.
- `Withdrawn` deletes the projection row; the product then matches on original price alone (FR-027).
