<!--
SYNC IMPACT REPORT
==================
Version change: 2.1.0 -> 2.2.0

Bump rationale: MINOR. A new principle section and new rules are added; the Technology Constraints
stack is widened to admit a frontend. No rule is withdrawn or redefined, and nothing compliant with
2.1.0 becomes non-compliant.

Added sections:
- X. Frontend (UIX)

Added rules:
- UIX-001 the frontend is a separate deployable, reaching no backend assembly, database, or broker
- UIX-002 the backend is consumed only through the published OpenAPI contract, via a generated
          client
- UIX-003 money is rendered from server-supplied minor units; client-side money arithmetic is
          FORBIDDEN. JavaScript has one numeric type and it is a float, so without this rule
          TXN-006's integer guarantee is lost at the boundary it exists to protect
- UIX-004 every interactive element is keyboard reachable and operable
- UIX-005 PrimeVue is the sole component library
- DEP-001 every deployable ships as a container image built by CI from a checked-in Dockerfile
- DEP-002 frontend and backend images are independently buildable and releasable

Added to the closed stack (STK-001):
- Vue and PrimeVue on Node LTS — the frontend named in this amendment. Versions are pinned in the
                                 frontend feature's plan.md, as ".NET 8" is pinned for the backend.
- Docker                       — DEP-001 makes container packaging the delivery mechanism for
                                 every deployable, which STK-001 did not previously permit.

Modified sections:
- Technology Constraints — stack split into backend and frontend; STK-001's enforcement extended
  to cover the frontend package manifest, since a JavaScript dependency tree is otherwise outside
  every check this document has.

Removed sections: none

Assumption recorded: the frontend lives in THIS repository as a separate deployable — a monorepo
producing two images. UIX-001 is worded to hold under a separate repository too, but its CI check
is only meaningful in a monorepo. Revisit the wording if the frontend moves out.

Follow-up TODOs (deferred — outside this command's scope):
- No frontend feature exists yet. The Vue/PrimeVue version pins, SSR-vs-SPA, state management,
  and routing all belong in that feature's plan.md, not here.
- specs/002-product-catalog/plan.md states "No frontend in this repository." Correct or mark
  superseded when the frontend feature lands.
- UIX-001 through UIX-005 and DEP-001/002 have no enforcement tests yet. Under GATE-001 a rule
  whose check does not run is unenforced; write the CI checks before relying on them.

--------------------------------------------------------------------------------
Previous report retained for history.

Version change: 2.0.0 -> 2.1.0

Bump rationale: MINOR. The Technology Constraints stack is expanded to name components that
002-product-catalog already runs on and that STK-001 did not list. No rule is withdrawn or
redefined; code compliant with 2.0.0 stays compliant.

Added to the closed stack (STK-001):
- gRPC and Protocol Buffers  — COM-001 requires a proto-defined cross-module read contract, so
  the rule already assumed a component the stack did not permit.
- Serilog                    — OBS-001 requires structured logging with correlation identifiers.
- ASP.NET Core health checks — ARC/REL redundancy needs a readiness signal the load balancer can
                               act on.
Rationale and enforcement for each are stated in Technology Constraints below.

--------------------------------------------------------------------------------
Previous report retained for history.

Version change: 1.0.0 -> 2.0.0

Bump rationale: MAJOR. TXN-004 is withdrawn and superseded by TXN-006 — a backward-incompatible
redefinition of an existing rule. Code that was compliant under TXN-004 (decimal money) is
non-compliant under TXN-006 (integer minor units). Per Governance, the identifier is not reused
or renumbered: TXN-004 stays in place marked WITHDRAWN.

Modified principles:
- V. Transactions and Sagas (TXN) — TXN-004 marked WITHDRAWN, superseded by TXN-006

Added rules:
- DAT-004  read paths via Dapper, writes via the owning DbContext
- DAT-005  raw SQL reads must apply the module visibility predicate via a shared fragment
- DAT-006  raw SQL must not reference tables outside its own schema
- TXN-006  money as integer minor units plus an explicit currency code
- SPC-001  spec.md must not name a technology
- STK-001  the Technology Constraints stack is closed
- OBS-001  customer-visible decisions must be logged with a reason code
- GATE-001 every rule's enforcement mechanism must run in CI and block the merge

Added sections:
- VIII. Specification Discipline (SPC)
- IX. Observability (OBS)
- Rule ID Crosswalk (transitional — REMOVED in 2.1.0; every feature document now cites
  canonical identifiers)

Removed sections: none

Rationale for SPC/STK/OBS/GATE: specs/002-product-catalog/plan.md and tasks.md assess against an
unratified "constitution v3.0.0" and cite roughly twenty identifiers that do not exist in this
document. Most are aliases of rules that are already here (see the crosswalk). These four name
concepts with no canonical equivalent and are enforceable, so they are adopted rather than aliased.
PRM-* is deliberately NOT adopted: those are Promotion business rules and belong in that feature's
spec, not in a document binding every module.

Follow-up TODOs (deferred — outside this command's scope):
- specs/002-product-catalog/plan.md and tasks.md: replace phantom identifiers per the crosswalk
  and correct the "constitution v3.0.0" claim to v2.0.0.
- src/Modules/Catalog/ECommerce.Catalog.Application/{Browse,Detail,Search,Filter}/*Query.cs use
  EF Core for reads and are non-compliant with DAT-004 as of this amendment. List them in the
  ARC-005 burn-down file or convert them.
- No ARC-004 architecture test exists in tests/ECommerce.ArchitectureTests/, and Product.cs reads
  DateTimeOffset.UtcNow, which the rule's stated enforcement (DateTime.UtcNow) does not catch.
  Not amended here; raise separately.
-->

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
- **DAT-004**: Read paths MUST execute through Dapper; writes MUST execute through the owning
  module's `DbContext`. A type named `*Query` MUST NOT call `SaveChanges`, `SaveChangesAsync`,
  `ExecuteUpdate*`, or `ExecuteDelete*`; a type named `*Command` MUST NOT execute raw SQL.
  *Enforced by: architecture test over Application and Infrastructure assemblies.*
- **DAT-005**: A raw SQL read MUST apply its module's visibility predicate through that module's
  shared query fragment. A hand-written visibility clause at a call site is FORBIDDEN. Dapper does
  not see EF Core global query filters, so DAT-004 removes the mechanism that previously guaranteed
  visibility on every read; this rule replaces it. *Enforced by: architecture test asserting every
  SQL literal selecting from a visibility-governed table includes the shared fragment; integration
  test per module asserting a non-visible row is absent from every read path.*
- **DAT-006**: Raw SQL MUST NOT reference a table outside its own module's schema. DAT-001's
  enforcement inspects `DbContext` mappings, which raw SQL bypasses. *Enforced by: CI script
  scanning SQL literals for schema-qualified names outside the owning schema.*

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
- **TXN-004** — **WITHDRAWN in v2.0.0, superseded by TXN-006.** Formerly: money MUST be represented
  as a decimal amount with an explicit currency. Withdrawn because a decimal money type still permits
  fractional amounts and rounding drift between the amount stored, the amount compared, and the
  amount displayed. TXN-006 makes both unrepresentable. Retained per Governance; MUST NOT be cited
  in new work.
- **TXN-005**: Totals MUST be computed server-side and never accepted from a client. *Enforced by:
  code review; request contracts carry no total fields.*
- **TXN-006**: Money MUST be represented as a 64-bit integer count of minor units plus an explicit
  ISO 4217 currency code. `decimal`, `double`, and `float` are FORBIDDEN on monetary members, in
  persisted columns, and in wire contracts. *Enforced by: architecture test banning those types on
  monetary members; CI migration scan asserting monetary columns are `bigint`; contract test
  asserting no floating-point or decimal field appears in a money message.*

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

### VIII. Specification Discipline (SPC)

- **SPC-001**: A feature `spec.md` MUST NOT name a framework, library, database, protocol, or
  technical pattern. Technology decisions belong in `plan.md`, where they can be revised without
  reopening what the feature is for. *Enforced by: CI keyword scan over `specs/*/spec.md`.*

### IX. Observability (OBS)

- **OBS-001**: Every decision that changes a customer-visible outcome — a discount applied, rejected,
  or fallen back on; a request refused; a fallback to stale data — MUST be logged with its
  correlation identifier and a reason code. *Enforced by: acceptance test per feature asserting the
  log entry exists carrying its reason code.*

### X. Frontend (UIX)

- **UIX-001**: The frontend is a separate deployable. It MUST NOT reference a backend assembly,
  open a database connection, or consume the message broker. Its only contact with the system is
  the backend's published HTTP contract. *Enforced by: CI check that the frontend dependency tree
  contains no database driver, no broker client, and no path into `src/`.*
- **UIX-002**: The frontend MUST consume the backend only through the published OpenAPI contract,
  via a client generated from it. Hand-written calls to undocumented paths are FORBIDDEN.
  *Enforced by: CI regenerating the client from `specs/*/contracts/*.openapi.yaml` and failing on a
  diff; lint rule banning HTTP calls outside the generated client.*
- **UIX-003**: Monetary amounts MUST be rendered from the server-supplied integer minor units and
  formatted only. Arithmetic on a monetary field in client code is FORBIDDEN. JavaScript has a
  single numeric type and it is a float; without this rule TXN-006's guarantee is defeated at the
  boundary it was written to protect. *Enforced by: lint rule banning arithmetic operators on the
  generated client's money type; component test asserting a discounted and an original price
  render exactly as the server supplied them.*
- **UIX-004**: Every interactive element MUST be reachable and operable by keyboard alone.
  *Enforced by: automated accessibility check in CI over every route.*
- **UIX-005**: PrimeVue is the sole component library. A second component library or CSS framework,
  or a hand-rolled replacement for a component PrimeVue provides, is FORBIDDEN. *Enforced by: CI
  allowlist check over the frontend package manifest.*

## Technology Constraints

- Backend: .NET 8, PostgreSQL, RabbitMQ, EF Core, Dapper. One solution, one deployable
  process.
- Frontend: Vue and PrimeVue on Node LTS. A separate deployable, released independently of
  the backend. Exact versions are pinned in the frontend feature's `plan.md`.
- Docker, for the container packaging DEP-001 requires of every deployable.
- gRPC and Protocol Buffers, for the cross-module read contract COM-001 requires. Message types
  are generated; the transport may be in-process before a module is extracted.
- Serilog, for the structured logging with correlation identifiers OBS-001 requires.
- ASP.NET Core health checks, for the readiness signal a redundant deployment needs. Readiness
  MUST NOT depend on a downstream module, or an outage there drains every instance — see REL-007.
- One schema per module in a single database; extraction to separate databases must require no
  application change beyond configuration.
- Warnings are errors. Analyzers and architecture tests run in CI on every pull request.
- **STK-001**: The stack above is closed. Adding a runtime component requires an amendment to this
  document naming the component, the rationale, and the enforcement mechanism. *Enforced by: CI
  check that every `PackageVersion` in `Directory.Packages.props` and every dependency in
  the frontend package manifest maps to an approved component.*
- **DEP-001**: Every deployable artifact MUST ship as a container image built by CI from a
  Dockerfile checked into the repository. Build or install steps performed outside the image
  are FORBIDDEN. *Enforced by: CI asserting each deployable has a Dockerfile and that its
  image builds from a clean checkout.*
- **DEP-002**: The frontend and backend images MUST be independently buildable and releasable.
  Neither build may require the other's toolchain or source. *Enforced by: CI building each
  image in an isolated job given only its own source tree.*

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
- Versioning is semantic. MAJOR for a backward-incompatible removal or redefinition, MINOR for a
  new rule or materially expanded guidance, PATCH for clarification.
- Detailed guidance MUST NOT accumulate here. It belongs in templates and skills, loaded on demand.
- Every pull request is reviewed against this document. Deviations require an explicit, recorded
  waiver naming the rule and its expiry.
- **GATE-001**: Every rule's stated enforcement mechanism MUST run in CI and block the merge. A rule
  whose check does not run is unenforced and MUST be withdrawn rather than left standing. *Enforced
  by: CI job inventory; a test asserting every rule identifier in this document has a
  correspondingly named test.*

**Version**: 2.2.0 | **Ratified**: 2026-09-04 | **Last Amended**: 2026-09-04
