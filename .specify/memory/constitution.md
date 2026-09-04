# E-Commerce Platform Constitution

The system is a **modular monolith**: one deployable process containing modules whose boundaries are
enforced mechanically, built so that any module can later be extracted into its own service without
a rewrite.

Every rule below states how it is enforced. **A rule that cannot be checked does not belong in this
document** — delete it rather than let it become decoration. Detailed guidance belongs in templates
and skills, not here; this file stays small enough to be read in full before every task.

Rule IDs are stable and immutable. Cite them in reviews.

## Core Principles

### I. Module Boundaries (ARC)

- **ARC-001**: A module MUST NOT reference any assembly of another module except that module's
  `.Contracts` assembly. *Enforced by: architecture test over assembly references.*
- **ARC-002**: A `.Contracts` assembly MUST contain only event schemas, generated proto types, and
  port interfaces. No entities, no persistence types, no handlers. *Enforced by: architecture test
  over public types in `*.Contracts`.*
- **ARC-003**: Business types MUST NOT be placed in shared or common projects. Shared code is limited
  to technical primitives — the test is "would this still make sense in a banking app?" *Enforced by:
  architecture test listing permitted namespaces in shared projects, plus code review.*
- **ARC-004**: Domain code MUST NOT read the system clock or generate identifiers directly; both are
  injected. *Enforced by: architecture test banning `DateTime.UtcNow` and `Guid.NewGuid()` in domain
  assemblies.*
- **ARC-005**: Known boundary violations MUST be listed in a checked-in burn-down file. CI fails on
  any violation not in that file, and entries are removed over time, never added to casually.
  *Enforced by: CI comparing current violations against the file.*

### II. Data Ownership (DAT)

- **DAT-001**: Each module owns exactly one database schema and MUST NOT read or write another
  module's tables. *Enforced by: one DbContext per module with a fixed default schema; architecture
  test asserting no DbContext maps outside it.*
- **DAT-002**: Foreign keys across schemas are FORBIDDEN. *Enforced by: CI script scanning generated
  migrations.*
- **DAT-003**: Data owned by another module MUST be snapshotted at the time of the business event and
  MUST NOT be re-read when displaying historical records. *Enforced by: code review; a price change
  must never alter a past invoice.*

### III. Communication (COM)

- **COM-001**: Synchronous cross-module reads MUST go through a proto-defined contract and a port
  interface owned by the consumer, bound in composition to an in-process adapter today and a gRPC
  client after extraction. *Enforced by: architecture test — no module type may be constructed
  directly by another module.*
- **COM-002**: Synchronous call depth MUST be 1. A module serving a cross-module call MUST NOT make
  another cross-module synchronous call. *Enforced by: code review; trace assertion in integration
  tests.*
- **COM-003**: A cross-module call MUST NOT enlist in the caller's database transaction.
  *Enforced by: integration test asserting separate connections.*
- **COM-004**: All cross-module writes MUST be asynchronous, via messages. *Enforced by: ARC-001 —
  there is no reference through which to call one.*
- **COM-005**: Events are past-tense facts, broadcast, and may have any number of consumers. Commands
  are imperative, point-to-point, and may be rejected. *Enforced by: naming convention check in CI.*
- **COM-006**: Every message MUST carry `message_id`, `type`, `version`, `occurred_at`,
  `correlation_id` and `causation_id`. *Enforced by: envelope type in shared messaging code; contract
  test per publisher.*
- **COM-007**: Event names MUST follow `<module>.<aggregate>.<past-tense-verb>.v<N>`. *Enforced by:
  CI naming check.*
- **COM-008**: A breaking change to a published schema requires a new version; the previous version
  stays until every consumer has migrated. *Enforced by: CI schema-compatibility check against the
  main branch.*

### IV. Reliable Messaging (REL) — NON-NEGOTIABLE

- **REL-001**: Publishing to the message broker from a handler is FORBIDDEN. Messages are written to
  the module's outbox table inside the business transaction. *Enforced by: architecture test banning
  broker-publishing types outside the relay.*
- **REL-002**: The relay MUST drain the outbox using `FOR UPDATE SKIP LOCKED` so multiple instances
  can run safely. *Enforced by: integration test with concurrent relays asserting each message is
  published once.*
- **REL-003**: Delivery is at-least-once. Every consumer MUST deduplicate via an inbox keyed on
  `(message_id, consumer)`, inserted in the same transaction as the business effect. *Enforced by:
  consumer base class; test asserting a replayed message produces one effect.*
- **REL-004**: Handlers MUST NOT assume message ordering. *Enforced by: test delivering a module's
  messages out of order.*
- **REL-005**: Consumers MUST be tolerant readers — unknown fields are ignored, never rejected.
  *Enforced by: contract test per consumer with an added unknown field.*
- **REL-006**: Every queue MUST have a dead-letter queue and a documented replay procedure.
  *Enforced by: infrastructure review; a runbook exists per queue.*
- **REL-007**: An outage in a downstream capability MUST NOT block the business action that produced
  the message. *Enforced by: acceptance test with the dependency unavailable.*

### V. Transactions and Sagas (TXN)

- **TXN-001**: One aggregate per transaction. Distributed transactions are FORBIDDEN. *Enforced by:
  code review; no ambient transaction spans two DbContexts.*
- **TXN-002**: Cross-module workflows MUST be orchestrated sagas with state persisted on every
  transition, optimistic concurrency on the saga row, and an enforced deadline. *Enforced by: saga
  persistence test; timeout test.*
- **TXN-003**: Every saga branch MUST have a compensation, and every compensation MUST have a test.
  *Enforced by: test-per-branch coverage check.*
- **TXN-004**: Money MUST be represented as a decimal amount with an explicit currency; floating
  point is FORBIDDEN for monetary values. *Enforced by: architecture test banning `double`/`float`
  on monetary members.*
- **TXN-005**: Totals MUST be computed server-side and never accepted from a client. *Enforced by:
  code review; request contracts carry no total fields.*

### VI. Security (SEC)

- **SEC-001**: Passwords MUST be at least 12 characters and screened against known-breached password
  lists. Periodic forced rotation is FORBIDDEN. *Enforced by: registration validation tests,
  including one asserting a known-breached password is rejected.*
- **SEC-002**: Credentials MUST be stored using a memory-hard hashing function with a per-credential
  salt. Plaintext or reversible storage is FORBIDDEN. *Enforced by: test asserting two accounts with
  the same password store different values, and that the stored value is not the password.*
- **SEC-003**: Authentication and account responses MUST NOT disclose whether a given identifier
  exists. *Enforced by: test asserting identical responses for unknown identifier and wrong
  credential.*
- **SEC-004**: Authorization MUST be checked on both the caller's role and the specific resource
  being accessed. *Enforced by: test attempting cross-account access for every owned resource.*
- **SEC-005**: Security-relevant events MUST be recorded with their time and origin, and retained for
  a stated period. *Enforced by: acceptance test per feature that records such events.*
- **SEC-006**: All input MUST be validated server-side at the application boundary, regardless of
  client-side validation. *Enforced by: contract tests submitting invalid payloads.*

### VII. Test-First (QAG) — NON-NEGOTIABLE

- **QAG-001**: Tests are written from acceptance criteria before implementation and MUST be observed
  to fail first. *Enforced by: review of commit order; a test that has never failed proves nothing.*
- **QAG-002**: Each acceptance criterion in a spec becomes a test name. *Enforced by: review against
  the spec's Success Criteria and acceptance scenarios.*
- **QAG-003**: Domain tests MUST NOT touch infrastructure. *Enforced by: architecture test on domain
  test assemblies.*
- **QAG-004**: Any write involving money or order state MUST have an idempotency test. *Enforced by:
  review checklist.*
- **QAG-005**: Any contended resource — stock, vouchers, balances — MUST have a concurrency test
  asserting exactly N of M parallel attempts succeed. *Enforced by: review checklist.*
- **QAG-006**: Infrastructure tests MUST run against real dependencies in containers, not fakes.
  *Enforced by: CI.*

## Technology Constraints

- .NET 8, PostgreSQL, RabbitMQ. One solution, one deployable process.
- One schema per module in a single database; extraction to separate databases must require no
  application change beyond configuration.
- Warnings are errors. Analyzers and architecture tests run in CI on every pull request.

## Development Workflow

The order is not negotiable; each step's output is the next step's input.

1. **Orient** — locate the owning module. A feature spanning two modules is under-analysed.
2. **Contracts** — commit proto and event schemas first, in their own change.
3. **Red** — write tests from the acceptance criteria and observe them fail.
4. **Domain** → **Application** → **Infrastructure** → **API**, each until its tests pass.
5. **Observe** — metrics, alerts and a runbook before rollout.
6. **Review** — cite rule IDs, not paraphrases.

## Governance

This constitution supersedes other practices. Where it conflicts with a template, a skill, or a
generated plan, this document wins.

- Amendments require a pull request that states the rule ID affected, the rationale, and the
  enforcement mechanism. An amendment adding an unenforceable rule MUST be rejected.
- Rule IDs are never reused or renumbered. A withdrawn rule is marked withdrawn and kept.
- Detailed guidance MUST NOT accumulate here. It belongs in templates and skills, loaded on demand.
- Every pull request is reviewed against this document. Deviations require an explicit, recorded
  waiver naming the rule and its expiry.

**Version**: 1.0.0 | **Ratified**: 2026-09-04 | **Last Amended**: 2026-09-04
