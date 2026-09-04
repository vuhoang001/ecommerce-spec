<!--
SYNC IMPACT REPORT
==================
Version change: none → 1.0.0
Bump rationale: MAJOR (initial adoption). The constitution was an unfilled template; this is
the first ratified version. All seven principles and both additional sections are new.

Added principles:
  I.   Module Boundaries (MOD-001..MOD-004)
  II.  Data Ownership (DAT-001..DAT-004)
  III. Cross-Module Communication (COM-001..COM-005)
  IV.  Reliable Messaging (NON-NEGOTIABLE) (REL-001..REL-006)
  V.   Transactions and Sagas (TXN-001..TXN-004)
  VI.  Message Format and Versioning (MSG-001..MSG-004)
  VII. Test-First (NON-NEGOTIABLE) (TST-001..TST-003)

Added sections:
  - Technology and Platform Constraints (STK-001..STK-004)
  - Development Workflow and Quality Gates (GATE-001..GATE-005)
  - Governance (GOV-001..GOV-006)

Removed sections: none (template placeholders replaced in full)

Rule identifiers are stable. A rule is never renumbered or reused; a withdrawn rule keeps its
identifier and is marked WITHDRAWN in place (GOV-004).

Deferred items requiring author confirmation:
  Four source lines arrived truncated mid-sentence. The rules below were reconstructed from the
  surrounding context and MUST be confirmed or corrected in a follow-up amendment:
    - DAT-003  ("Data owned by another contex time of the business event ...")
    - COM-001  ("Synchronous cross-module read contract and a port interface owned by the
                 consuocess adapter now and a gRPC client after extraction")
    - COM-002  ("... MUST NOT make another cross-module synchro")
    - COM-004  ("All cross-module writes are nts are past-tense facts, broadcast; ...")
-->

# E-Commerce Platform Constitution

This constitution governs an e-commerce platform built as a modular monolith: one deployable
process, modules with enforced boundaries, designed so that any module can later be extracted
into an independent service without redesigning its contracts, its data, or its callers.

Every rule carries a stable identifier. Code reviews, architecture tests, pull request
descriptions, and specifications MUST cite the identifier rather than paraphrasing the rule.

Requirement levels (MUST, MUST NOT, SHOULD, MAY) follow RFC 2119. A rule marked
**NON-NEGOTIABLE** MUST NOT be waived by the complexity-justification path in GOV-003.

## Core Principles

### I. Module Boundaries

A module is a compilation and ownership unit. Extraction into a service must be a deployment
change, not a redesign — so the boundary is enforced at build time, not by convention.

- **MOD-001**: A module MUST NOT reference any assembly of another module except that module's
  `.Contracts` assembly. Verified by an architecture test that walks assembly references and
  fails the build on any other cross-module reference.
- **MOD-002**: A `.Contracts` assembly MUST contain only event schemas, generated protobuf
  types, and port interfaces. Entities, EF Core types (`DbContext`, entity configurations,
  migrations), and message handlers are FORBIDDEN in `.Contracts`. Verified by an architecture
  test over the public and internal types of every `*.Contracts` assembly.
- **MOD-003**: Shared projects MUST be limited to technical primitives that would still make
  sense unchanged in a banking application — clock abstractions, result types, identifiers,
  serialization helpers, and similar. Business types are FORBIDDEN in shared projects.
  A type is a business type when its name or behaviour only makes sense in commerce
  (`Order`, `Cart`, `Product`, `Voucher`, `Price`, `Stock`, and the like).
- **MOD-004**: A module MUST NOT expose its internal types through its `.Contracts` assembly by
  inheritance, generic parameter, or public member signature. Verified by the same architecture
  test as MOD-002.

**Rationale**: If a module can reach another module's internals, extraction later requires
rewriting call sites under deadline pressure. Enforcing the boundary now makes extraction a
mechanical operation.

### II. Data Ownership

Data has exactly one owner. Reading another module's tables creates a coupling no architecture
test at the assembly level can see.

- **DAT-001**: Each module owns exactly one PostgreSQL schema. A module MUST NOT read or write
  another module's tables by any means — direct SQL, a second `DbContext`, a view, or a
  database function.
- **DAT-002**: Foreign keys that cross schema boundaries are FORBIDDEN. Verified by a migration
  check that inspects generated DDL and fails when a `REFERENCES` clause names a table outside
  the migrating module's schema.
- **DAT-003**: Data owned by another context MUST be copied into the consuming module at the
  time of the business event and MUST NOT be re-read from the owning module for historical
  display. An order line records the price, name, and tax rate that applied when the order was
  placed; it never re-reads the catalogue to render an old order.
  *(Reconstructed from truncated input — confirm wording.)*
- **DAT-004**: A module's database credentials MUST be scoped so that access to another module's
  schema fails at the database, not only in review.

**Rationale**: DAT-003 is what makes an order immutable in the way a business expects. A price
change in the catalogue must never silently rewrite what a customer was charged last month.

### III. Cross-Module Communication

Two modules may talk in exactly two ways: a synchronous read through an owned port, or an
asynchronous message. Nothing else crosses a module boundary.

- **COM-001**: A synchronous cross-module read MUST go through a read contract and a port
  interface owned by the consuming module. The port is implemented by an in-process adapter
  today and by a gRPC client after extraction; the consumer's code MUST NOT change when the
  implementation is swapped. *(Reconstructed from truncated input — confirm wording.)*
- **COM-002**: Synchronous call depth MUST be 1. A module serving a cross-module synchronous
  call MUST NOT make another cross-module synchronous call while serving it.
  *(Reconstructed from truncated input — confirm wording.)*
- **COM-003**: A cross-module call MUST NOT enlist in the caller's database transaction. The
  caller MUST have committed or MUST NOT yet have opened its transaction when the call is made.
- **COM-004**: All cross-module writes MUST be asynchronous and carried by messages. A module
  MUST NOT change another module's state through a synchronous call.
  *(Reconstructed from truncated input — confirm wording.)*
- **COM-005**: Events are past-tense facts, broadcast to any number of consumers, and MUST NOT
  be rejected by a consumer as invalid — the fact already happened. Commands are imperative,
  point-to-point with exactly one owning handler, and MAY be rejected.

**Rationale**: COM-002 bounds the blast radius of a slow module. Without it, one degraded
module stalls a call chain of unbounded length, and the modular monolith fails the way a
distributed monolith fails.

### IV. Reliable Messaging (NON-NEGOTIABLE)

The broker is not part of the business transaction. Every guarantee below exists because a
process can die between the database commit and the publish.

- **REL-001**: Publishing to the broker directly from a handler is FORBIDDEN. Messages MUST be
  written to the publishing module's outbox table inside the same database transaction as the
  business effect that produced them.
- **REL-002**: A background relay process MUST publish outbox rows to RabbitMQ, claiming rows
  with `SELECT ... FOR UPDATE SKIP LOCKED` so that concurrent relay instances never publish the
  same row twice.
- **REL-003**: Delivery is at-least-once. Every consumer MUST deduplicate through an inbox
  table keyed on `(message_id, consumer)`, inserted in the same transaction as the business
  effect. A duplicate delivery MUST leave state unchanged and MUST NOT raise an error.
- **REL-004**: Handlers MUST NOT assume message ordering. A handler that requires ordering MUST
  derive it from data in the message (a version, a sequence number, or `occurred_at`), never
  from arrival order.
- **REL-005**: Consumers are tolerant readers. Unknown fields MUST be ignored, never rejected.
- **REL-006**: Every queue MUST have a dead-letter queue and a replay procedure documented in
  the owning module's runbook. A queue without a documented replay procedure MUST NOT reach
  production.

**Rationale**: At-least-once delivery with an inbox is achievable; exactly-once is not. Every
rule here converts an unreliable network into a correct business outcome.

### V. Transactions and Sagas

- **TXN-001**: One aggregate per transaction. A transaction MUST NOT modify two aggregates.
- **TXN-002**: Distributed transactions are FORBIDDEN. Two-phase commit, `TransactionScope`
  spanning more than one resource, and any transaction spanning the database and the broker
  MUST NOT be used.
- **TXN-003**: A workflow spanning more than one module MUST be an orchestrated saga with
  persisted state, optimistic concurrency on the saga row, an enforced deadline, and a
  compensation for every branch.
- **TXN-004**: Every compensation branch MUST have a test that drives the saga into that branch
  and asserts the compensated state. A saga with an untested compensation branch MUST NOT be
  merged.

**Rationale**: A saga's compensation paths run rarely and under failure, which is exactly when
nobody is watching. Untested compensation is the single most reliable source of stuck money.

### VI. Message Format and Versioning

- **MSG-001**: Every message MUST carry `message_id`, `type`, `version`, `occurred_at`,
  `correlation_id`, and `causation_id`. A message missing any of these MUST be rejected at the
  transport boundary.
- **MSG-002**: Event names MUST follow `<context>.<aggregate>.<past-tense-verb>.v<N>` — for
  example `ordering.order.placed.v1`.
- **MSG-003**: A breaking change to an event schema MUST be published as a new version. The
  previous version MUST continue to be published until every consumer has migrated off it.
  Verified by a CI schema compatibility check that compares each schema against the version on
  the main branch and fails on a breaking change without a version increment.
- **MSG-004**: Retiring an event version requires evidence that no consumer subscribes to it,
  recorded in the pull request that removes it.

**Rationale**: `correlation_id` and `causation_id` are what make a failure traceable across
modules once the call graph is asynchronous.

### VII. Test-First (NON-NEGOTIABLE)

- **TST-001**: Tests are written from acceptance criteria before implementation and MUST be
  observed to fail first. A pull request MUST show the failing test before the implementing
  commit, or state in its description how the failure was observed.
- **TST-002**: The following MUST exist before implementation begins:
  - domain invariant tests for every invariant the aggregate enforces;
  - one test per acceptance criterion in the specification;
  - idempotency tests for any write that moves money or changes an order;
  - concurrency tests for stock, vouchers, and balances, asserting correct behaviour under
    parallel writers.
- **TST-003**: A test MUST assert an observable business outcome. Asserting that a method was
  called does not satisfy TST-002.

**Rationale**: Stock, vouchers, and balances are the fields where a lost update is money. A
test written after the implementation encodes the implementation's assumptions rather than the
business's.

## Technology and Platform Constraints

- **STK-001**: The platform targets .NET 8, PostgreSQL, and RabbitMQ. Introducing another
  database engine or broker requires an amendment under GOV-002.
- **STK-002**: The system ships as a modular monolith — one deployable process. A module MUST
  be structured so that extraction into a service changes deployment and transport only.
- **STK-003**: Cross-module transport abstractions MUST have both an in-process implementation
  and a defined out-of-process implementation path (gRPC for reads, RabbitMQ for messages).
- **STK-004**: Module boundaries, schema ownership, and event ownership MUST be recorded in a
  context map kept current with the code.

## Development Workflow and Quality Gates

- **GATE-001**: The architecture test suite (MOD-001, MOD-002, MOD-004) runs on every build.
  A failing architecture test blocks the merge.
- **GATE-002**: The migration check (DAT-002) runs on every pull request that adds a migration.
- **GATE-003**: The event schema compatibility check (MSG-003) runs on every pull request that
  changes a `.Contracts` assembly.
- **GATE-004**: Code review MUST cite rule identifiers. A review comment that rejects code on
  architectural grounds without naming a rule identifier is not actionable, and the author MAY
  ask for the identifier or for an amendment.
- **GATE-005**: A pull request that cannot satisfy a rule MUST either be redesigned or carry an
  explicit exception recorded under GOV-003. Silent non-compliance is a defect.

## Governance

- **GOV-001**: This constitution supersedes all other development practices, style guides, and
  team conventions. Where a practice conflicts with a rule here, the rule wins.
- **GOV-002**: An amendment requires a pull request that changes this file, states the rules
  added, modified, or withdrawn, and states the migration plan for code that the change makes
  non-compliant. Amendments follow semantic versioning:
  - MAJOR — a rule is removed or redefined in a way existing compliant code can fail;
  - MINOR — a rule or section is added, or existing guidance is materially expanded;
  - PATCH — clarification, wording, or a non-semantic refinement.
- **GOV-003**: An exception to a rule that is not marked NON-NEGOTIABLE MUST be recorded in the
  pull request with the rule identifier, the reason, and the removal plan with an owner and a
  date. Rules in Principles IV and VII are NON-NEGOTIABLE and admit no exception.
- **GOV-004**: Rule identifiers are stable. A rule is never renumbered and an identifier is
  never reused. A withdrawn rule keeps its identifier and is marked WITHDRAWN in place, with
  the version that withdrew it.
- **GOV-005**: Compliance is reviewed at every pull request through the gates in GATE-001
  through GATE-005. A rule that cannot be verified by a test, a gate, or a review checklist
  item is a defect in this constitution and MUST be either made verifiable or withdrawn.
- **GOV-006**: Runtime development guidance for agents and contributors lives in `CLAUDE.md` at
  the repository root. It MUST NOT contradict this constitution; where it does, this file wins
  and the guidance MUST be corrected.

**Version**: 1.0.0 | **Ratified**: 2026-09-04 | **Last Amended**: 2026-09-04
