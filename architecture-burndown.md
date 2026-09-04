# Architecture Burn-Down

**Satisfies**: `ARC-005` — known boundary violations MUST be listed in a checked-in burn-down
file; CI fails on additions.

This file records deviations that are **known, deliberate, and being worked off**. It is not a
place to park things indefinitely: every entry carries an owner and the condition that closes it.
An entry with neither is a defect in this file.

## Open

| ID | Rule | Deviation | Why it stands | Closes when | Owner |
|---|---|---|---|---|---|
| BD-001 | `SC-015`, `SC-016` | The recovery drill has never been run. Single-instance kill, total-outage restart timing, and database failover are all unmeasured. | All three need a deployed, load-balanced environment with managed PostgreSQL, which does not exist yet. Exercise 4 (Promotion outage) *is* covered automatically. | `docs/runbooks/catalog-recovery-drill.md` has measured timings for exercises 1–3. | unassigned |
| BD-002 | `SC-003`, `SC-004` | The load test is written but never executed, so the latency and search-success targets are unmeasured. | Needs a catalogue seeded to 100,000 products and a k6 runner. | `tests/performance/catalog-load-test.js` has been run at target scale and the p95 recorded. | unassigned |
| BD-003 | `REL-006` | Queues have no documented replay procedure, and no dead-letter queue is configured. | Catalog registers zero publishers and one consumer; MassTransit's error queue is the DLQ but the replay procedure is unwritten. | A replay procedure exists in the module runbook and a test drains the error queue. | unassigned |
| BD-004 | `COM-001` | The cross-module read is served by an in-process adapter, not a gRPC client. | `research.md` R5: inside one process a loopback gRPC call costs serialisation and buys no isolation, because both modules already share a failure domain. The proto contract and the consumer-owned port are in place, so the swap is one class. | The promotion module is extracted, or the decision is reversed. | unassigned |
| BD-005 | `PRM-001`…`PRM-004` | `FR-011` and `Prm001NoDiscountCalculationTests` encode a rule the constitution did **not** adopt. | The behaviour is correct — Catalog genuinely never calculates a discount — but the rule belongs in the promotion feature's spec, per the Rule ID Crosswalk. | The promotion feature's spec owns the rule and the test is renamed to cite it. | unassigned |

## Closed

| ID | Rule | Deviation | Closed by |
|---|---|---|---|
| BD-006 | `DAT-004` | All four read paths executed through EF Core instead of Dapper. | Migrated to Dapper; `Dat004ReadWriteSeparationTests` now enforces it. |
| BD-007 | `DAT-005` | Read visibility relied on the EF Core global query filter, which Dapper cannot see. | `CatalogVisibility` shared fragment; `Dat005VisibilityFragmentTests` scans every read. |
| BD-008 | `DAT-006` | `OutboxClaim` hardcoded `catalog.outbox_message` in shared infrastructure. | Schema is now a parameter; `scripts/check-sql-schemas.sh` scans for the pattern. |
| BD-009 | `ARC-004` | `Product.Create` fell back to `DateTimeOffset.UtcNow`. | Timestamp is now a required parameter; `Arc004NoAmbientClockOrIdTests` enforces it. |

## Rules deliberately assessed as not applicable

| Rule | Judgement |
|---|---|
| `SEC-001`, `SEC-002` | No credential is created, stored, or verified by this feature. Password length and hashing bind whichever feature owns authentication. |
| `SEC-003` | No authentication or account response exists here. **The underlying concern is met anyway**: `FR-002` requires a Hidden product to be reported identically to one that never existed, and `ProductDetailVisibilityTests` asserts the two responses are byte-identical. |
| `SEC-004` | The catalogue is deliberately anonymous (`FR-034`, `SC-013`) and exposes no per-resource permission, so there is no role or resource to check. Revisit the moment any endpoint reads a customer identity. |
| `SEC-005` | No security-relevant event occurs on an anonymous read path. Promotion decisions are logged under `OBS-001`, which is an operational, not a security, record. |
| `SEC-006` | **Applies and is satisfied.** Every input is validated server-side at the boundary: `PriceRangeValidator` (range), keyword validation (search), `EnvelopeValidator` (messages), and route constraints on identifiers. |
| `TXN-002`, `TXN-003` | No saga exists; this feature has no cross-module workflow. |
| `QAG-004`, `QAG-005` | No money or order **write** and no contended resource exist here — stock is read and never changed. The discount-copy write is covered by an idempotency test regardless (`InboxDeduplicationTests`). |
