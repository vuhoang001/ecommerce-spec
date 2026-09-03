<!--
FEATURE SPEC TEMPLATE  v1.0.0
=============================
Copy this file to  specs/<NNN>-<feature-slug>/spec.md  and fill every section.

Rules for using this template:
  • Delete guidance comments (<!-- … -->) as you fill each section. A spec still carrying
    guidance text is not finished.
  • A section that genuinely does not apply is marked "N/A — <one-line reason>".
    It is NOT deleted. A missing section is indistinguishable from a forgotten one.
  • Every [BRACKETED_PLACEHOLDER] must be replaced or explicitly marked
    "TODO(<what>): <who resolves it, by when>".
  • Cite constitution rule IDs (ARC-002, REL-002, EDG-003 …) rather than paraphrasing them.

Governed by: specs/constitution.md v2.0.0
-->

# [FEATURE NAME]

| | |
|---|---|
| **Spec ID** | `[NNN]-[feature-slug]` |
| **Status** | Draft \| In Review \| Approved \| Implemented \| Superseded |
| **Author** | [name] |
| **Created** | [YYYY-MM-DD] |
| **Last updated** | [YYYY-MM-DD] |
| **Bounded Context** | [ONE context from `specs/contexts.md` § 3] |
| **Strategic class** | Core \| Supporting \| Generic *(per `contexts.md` § 1.2)* |
| **Reviewers** | [owning context maintainer] · [each consuming context maintainer — COM-041] |

---

## 1. Summary

<!-- Two to four sentences. What changes for a user or an operator, and why now?
     Written so someone outside the team understands it. No implementation detail here. -->

[SUMMARY]

### 1.1 Problem

<!-- What is broken, missing, or costly today? Include evidence: a metric, a support-ticket
     volume, a revenue number, a named incident. "Users want it" is not evidence. -->

[PROBLEM]

### 1.2 Goals

- [Measurable outcome 1 — e.g. "checkout abandonment on the payment step drops below 8%"]
- [Measurable outcome 2]

### 1.3 Non-Goals

<!-- Explicitly out of scope. This section prevents scope creep during implementation and
     tells reviewers what NOT to ask for. -->

- [Deliberately excluded 1 — and why]
- [Deliberately excluded 2]

---

## 2. Bounded Context Assignment

**Owning context**: `[CONTEXT]`

<!-- ARC-010: exactly ONE context owns this capability. If the feature seems to need two,
     STOP — resolve the boundary in specs/contexts.md § 7 BEFORE writing this spec.
     A feature spec is not the place to discover a boundary problem. -->

**Justification**: [Why this context owns it, per `contexts.md` § 3 responsibilities.]

**Contexts affected as consumers**: [list, or "none"]

**Does this change the context map?** No \| Yes → *this spec MUST NOT proceed until
`contexts.md` is amended (its § 7).*

---

## 3. Ubiquitous Language

<!-- Every domain term this feature introduces or relies on. If a term already appears in
     contexts.md § 2 with a different meaning in another context, say so explicitly (ARC-013).
     Code, tests, API fields, event fields and this spec MUST all use these exact words. -->

| Term | Definition in this context | Notes / conflicts elsewhere |
|---|---|---|
| [Term] | [Precise definition — no synonyms, no hedging] | [e.g. "Ordering calls this `Customer`; Identity calls the same person `UserAccount`"] |
| | | |

**Terms deliberately NOT used**: [e.g. "'user' — ambiguous across contexts; use `Customer`."]

---

## 4. Domain Model

### 4.1 Aggregate Roots

<!-- TXN-001: the aggregate is the consistency boundary. List invariants that MUST hold at
     every commit. An invariant that spans two aggregates is NOT an invariant — it is a saga
     (Article VI) or a misplaced boundary. -->

| Aggregate | New or existing | Invariants enforced |
|---|---|---|
| `[Aggregate]` | New \| Modified | • [Invariant 1]<br>• [Invariant 2] |

### 4.2 Entities & Value Objects

| Type | Kind | Purpose | Notes |
|---|---|---|---|
| `[Name]` | Entity \| Value Object | [purpose] | [e.g. "immutable; TXN-011 money as decimal + currency"] |

### 4.3 State Machine

<!-- Required if this feature introduces or changes a lifecycle. EDG-032: illegal transitions
     MUST be rejected by the aggregate, not merely hidden in the UI. Delete if N/A. -->

```
[StateA] ──[trigger]──► [StateB] ──[trigger]──► [StateC]
    │                                              │
    └──────────[trigger]──► [StateTerminal] ◄──────┘
```

**Illegal transitions explicitly rejected**: [e.g. `Cancelled → Shipped`]

### 4.4 Domain Events *(internal)*

<!-- COM-032: domain events are internal and free to change. They are NOT what goes on the
     broker — see § 7 for integration events. -->

| Domain event | Raised when | Handled by |
|---|---|---|
| `[AggregateChangedEvent]` | [condition] | [in-process handler / translated to integration event] |

---

## 5. Commands & Queries

<!-- ARC-030: commands mutate and return only an identifier + result; queries are side-effect
     free. Never both in one operation. -->

### 5.1 Commands

| Command | Actor | Preconditions | Effect | Idempotent? |
|---|---|---|---|---|
| `[CommandName]` | [role] | [what must be true] | [state change] | Yes via `Idempotency-Key` (EDG-020) \| Naturally \| **No — justify** |

### 5.2 Queries

| Query | Actor | Returns | Source | Paginated? |
|---|---|---|---|---|
| `[QueryName]` | [role] | [shape] | Aggregate \| Read model (ARC-032) | Yes (COM-014) \| N/A |

**Read model consistency** *(if any query reads a projection)*: eventually consistent,
target p50 [X] / p99 [Y]. User-visible behaviour while divergent: [describe — TXN-020, TXN-021].

---

## 6. API Contracts

### 6.1 REST *(external edge only — COM-011)*

<!-- Delete if this feature exposes no public API. -->

#### `[METHOD] /api/v1/[resource]`

**Auth**: [required role/scope — SEC-021] · **Resource authorization**: [ownership check — SEC-022]
**Idempotency**: `Idempotency-Key` required \| not applicable *(required for any money or order write — EDG-020)*
**Rate limit**: [N req/min per [subject]] *(counters in Redis — ARC-041)*

**Request**

```jsonc
{
  "[field]": "[type]"   // [constraint; server-side validation required — SEC-031]
}
```

**Responses**

| Status | Condition | Body |
|---|---|---|
| `200`/`201` | Success | `[shape]` |
| `400` | Validation failed | Error envelope with `errors[]` (COM-012) |
| `401` / `403` | Unauthenticated / unauthorized | Error envelope |
| `404` | Not found or not owned by caller *(SEC-022: do not leak existence)* | Error envelope |
| `409` | [conflict condition] | Error envelope, `code: [STABLE_CODE]` |
| `422` | Idempotency key reused with a different body (EDG-022) | Error envelope |

**Error codes introduced** *(stable, machine-readable — COM-013)*:
`[CONTEXT_CONDITION_NAME]` — [meaning]

### 6.2 gRPC *(internal synchronous reads only — COM-020, COM-024)*

<!-- Delete if none. Remember: gRPC MUST NOT mutate another context's state (COM-024). -->

```protobuf
// contracts/proto/[context]/v1/[file].proto
service [ServiceName] {
  rpc [Method]([Request]) returns ([Response]);   // read-only
}
```

**Caller deadline**: [X ms] *(COM-022 — mandatory)*
**Call-chain depth introduced**: [N] *(COM-023 — MUST be ≤ 2)*
**Behaviour when the circuit breaker is open**: [fallback — RES-023]

---

## 7. Integration Events

<!-- Governed by specs/event-governance.md. Every event here MUST have a JSON Schema
     committed under contracts/events/ in the same PR (EVG-020) and MUST appear in the
     event catalog (EVG-054). -->

### 7.1 Published

#### `[context].[aggregate].[past-tense-verb].v1`

**Trigger**: [what state change causes it]
**Consumers**: [contexts, or "none yet — published for future use"]
**Payload style**: Event-carried state transfer \| Thin \| Delta *(EVG-025, EVG-026)*
**Ordering required?** Yes — partition by `[aggregateId]` (REL-006) \| No
**PII carried**: [none \| field + justification — EVG-028]

```jsonc
{
  "specversion": "1.0",
  "id":          "[ULID]",
  "type":        "[context].[aggregate].[verb].v1",
  "source":      "/[service]",
  "subject":     "[aggregateId]",
  "time":        "[RFC3339 UTC]",
  "dataschema":  "https://contracts.example.com/events/[context]/[file].v1.json",
  "correlationid": "[…]", "causationid": "[…]", "aggregatetype": "[Aggregate]",
  "traceparent":   "[W3C]",
  "data": {
    "[field]": "[value]"   // money as STRING (EVG-003); currency at enclosing scope (EVG-004)
  }
}
```

### 7.2 Consumed

| Event | From | Reaction | Idempotency mechanism | Out-of-order handling |
|---|---|---|---|---|
| `[event.v1]` | [context] | [what this context does] | Inbox (REL-021) \| Naturally idempotent — *justify* (REL-023) | Version guard (REL-027) \| N/A — ordering guaranteed |

**Tolerant reader confirmed**: unknown fields ignored, unknown enum values handled without
throwing (EVG-040, EVG-042). ☐

---

## 8. Distributed Transaction / Saga

<!-- Required if this feature spans two or more contexts (SAG-001). Otherwise:
     "N/A — this feature is contained within a single context."
     SAG-002: the saga MUST be specified here BEFORE it is implemented. -->

**Applies?** No — single context \| **Yes** → complete the table below.

**Orchestrator**: `[context]` *(SAG-012 — MUST own the business outcome)*
**Style**: Orchestration *(required for money/inventory/customer commitment — SAG-010)* \| Choreography *(SAG-011 conditions met: [state them])*
**Overall deadline**: [X] *(SAG-030)* · **Per-step timeout**: [Y]

| # | Step | Context | Command | Success event | Failure event | Compensating action | Compensable? |
|---|---|---|---|---|---|---|---|
| 1 | [step] | [ctx] | `[cmd]` | `[ok.v1]` | `[fail.v1]` | `[compensation]` | Yes |
| 2 | | | | | | | Yes |
| 3 | [e.g. dispatch] | | | | | — | **No — MUST be last (SAG-029)** |

**Compensation ordering**: reverse of completed steps (SAG-028).
**Compensation idempotency**: every compensating action above is idempotent and retryable
(SAG-027). ☐
**Manual intervention path** *(required for money-bearing sagas — SAG-032)*: [describe]
**Stuck-saga detection**: [alert + metric — SAG-031]

---

## 9. Business Edge Cases

<!-- Article VII. This section is where the money is lost. Be specific; "handled by the
     framework" is not an answer. -->

### 9.1 Concurrency

| Scenario | Risk | Strategy | Rule |
|---|---|---|---|
| [Two customers buy the last unit simultaneously] | Overselling | Atomic conditional update — `WHERE available >= @qty` | EDG-003 (A) |
| [Same voucher redeemed twice in parallel] | Over-redemption | [Strategy A/B/C + justification] | EDG-031 |

**Stock strategy chosen**: A (atomic conditional) \| B (optimistic) \| C (pessimistic lock)
— **justification**: [required — EDG-003]

### 9.2 Idempotency & Retry

| Operation | Duplicate arrives via | Protection | Rule |
|---|---|---|---|
| [Command] | Double-click / client retry | `Idempotency-Key` + stored response replay | EDG-020, EDG-021 |
| [Consumer] | Broker redelivery | Inbox table | REL-021 |
| [Gateway call] | Ambiguous timeout | Deterministic gateway key `{orderId}:{attempt}` | EDG-024 |

**Ambiguous-write handling**: [what happens on a timeout where the outcome is unknown —
RES-002, EDG-025]

### 9.3 Failure Modes

| Dependency | If it fails | Behaviour | Fail open/closed | Rule |
|---|---|---|---|---|
| [dependency] | [timeout / down / 5xx] | [what the user sees] | Closed *(required for money & stock — RES-041)* | RES-023 |

**Retry policy**: [max attempts] attempts, exponential backoff base [X]s cap [Y]s with jitter,
total budget [Z] *(RES-010, RES-011)*.
**Retry owned by which layer**: [exactly one — RES-012]
**Circuit breaker**: threshold [X]% over [N] calls in [T]s, break [D]s *(RES-021, RES-022)*
**DLQ**: configured ☐ · replay procedure documented at [link] *(RES-030, RES-033)*

### 9.4 Other Edge Cases

<!-- Empty cart, deleted product mid-checkout, price change between add-to-cart and pay,
     partial refund exceeding capture, customer deactivated mid-order, timezone/DST,
     currency rounding, zero-quantity, negative amounts, expired reservation… -->

| Case | Expected behaviour |
|---|---|
| [case] | [behaviour] |

---

## 10. Acceptance Criteria

<!-- Given-When-Then, one scenario per behaviour. These become the test names verbatim (QAG-001).
     A criterion that cannot be executed as a test is not a criterion — rewrite it.
     Include the unhappy paths: they are where the defects live. -->

### AC-1 — [Happy path name]

```gherkin
Given [precise initial state — actual values, not "some data"]
  And [additional precondition]
 When [single action]
 Then [observable outcome — a specific value, status code, or state]
  And [side effect: event published / row written / notification queued]
```

### AC-2 — [Rejection / validation path]

```gherkin
Given [state]
 When [action violating a precondition]
 Then the request is rejected with [status] and code `[STABLE_CODE]`
  And no state change occurs
  And no integration event is published
```

### AC-3 — Idempotency *(required for every money/order write — QAG-005)*

```gherkin
Given [command] has already succeeded with Idempotency-Key "K"
 When the identical request is submitted again with Idempotency-Key "K"
 Then the stored response is replayed verbatim (EDG-021)
  And the effect occurs exactly once
  And no second integration event is published
```

### AC-4 — Concurrency *(required where contention exists — QAG-006)*

```gherkin
Given [N] units of "[SKU]" are available
 When [M] customers submit [command] simultaneously, where M > N
 Then exactly [N] succeed
  And exactly [M-N] are rejected with `[STABLE_CODE]`
  And available stock is exactly 0
  And no oversell is recorded (EDG-001)
```

### AC-5 — Message redelivery *(required for every consumer — QAG-005)*

```gherkin
Given event "[event.v1]" with id "X" has been processed
 When the identical message id "X" is delivered again
 Then the effect is not repeated (REL-020)
  And the message is acknowledged
```

### AC-6 — Compensation *(required per saga path — QAG-004)*

```gherkin
Given the saga has completed steps 1..[N]
 When step [N+1] fails with [reason]
 Then steps [N]..1 are compensated in reverse order (SAG-028)
  And the order reaches state [X]
  And the customer is notified with [message]
```

<!-- Add AC-7, AC-8 … for each remaining behaviour and edge case from § 9. -->

---

## 11. Non-Functional Requirements

| Dimension | Requirement | Measurement | Rule |
|---|---|---|---|
| **Latency** | p95 < [X] ms, p99 < [Y] ms | [endpoint/consumer] | OBS-030 |
| **Throughput** | [N] req/s sustained; [M] peak | Load test | QAG-009 |
| **Availability** | [99.9% if checkout path] | Uptime monitor | RES-042 |
| **Consistency** | Strong \| Eventual, converge p99 < [X] | [how verified] | TXN-010, TXN-020 |
| **Data retention** | [duration + purge job] | | REL-010, REL-025, EDG-027 |
| **Scalability** | Stateless; scales horizontally | | ARC-040 |

### 11.1 Observability

| Signal | Requirement | Rule |
|---|---|---|
| **Correlation** | `Correlation-ID` propagated across every hop including the broker | OBS-001, OBS-002, OBS-011 |
| **Metrics added** | [list — e.g. `orders_placed_total`, `stock_reservation_rejected_total`] | OBS-030 |
| **Alerts added** | [condition → severity → runbook link] | OBS-031, OBS-032 |
| **Log events** | [business milestones logged at Information] | OBS-023 |
| **Traceability** | The full flow is reconstructable from one `Correlation-ID` | OBS-034 |

**PII/secret leakage reviewed** in logs, traces, and event payloads. ☐ *(OBS-022, SEC-012)*

### 11.2 Security

| Concern | Requirement | Rule |
|---|---|---|
| **AuthN** | [mechanism] | SEC-020 |
| **AuthZ** | Role **and** resource ownership checked server-side | SEC-021, SEC-022 |
| **PII** | [fields touched; encryption at rest] | SEC-010 |
| **Card data** | None stored — [gateway + tokenization] | SEC-001 |
| **Input validation** | Server-side schema at the Application boundary | SEC-031 |

---

## 12. Data & Migration

**Schema**: `[context]` *(ARC-021)*

| Table | Change | Notes |
|---|---|---|
| `[table]` | New \| Altered | [columns, indexes, constraints] |

**Migration**: [name] · **Backfill required?** No \| Yes → [strategy, volume, duration]
**Rollback plan**: [how to reverse — or why it is forward-only]
**Cross-context FKs introduced**: **None** *(ARC-022 — MUST be none)*
**Soft-delete / audit trail**: [required for orders, payments, stock — TXN-022]

---

## 13. Constitution Check

<!-- G.4: cite the rules this design is built against. Any deviation MUST be listed with a
     waiver (EXT-010, EXT-011) including a hard expiry date — or the design MUST change.
     Rules in REL, EDG and SEC MUST NOT be waived (EXT-013). -->

| Article | Compliance | Notes |
|---|---|---|
| I — Architecture (`ARC`) | ☐ | Layering, domain isolation, DB-per-context, statelessness |
| II — Communication (`COM`) | ☐ | Style per COM-001 table; deadlines; contracts versioned |
| III — Reliability (`REL`) | ☐ | Outbox atomic; consumers idempotent; envelope complete |
| IV — Transactions (`TXN`) | ☐ | One aggregate per tx; no cross-context tx |
| V — Resilience (`RES`) | ☐ | Backoff, breaker, DLQ, degradation defined |
| VI — Saga (`SAG`) | ☐ | Compensation table complete; non-compensable last |
| VII — Edge cases (`EDG`) | ☐ | No read-then-write; idempotency keys enforced |
| VIII — Observability (`OBS`) | ☐ | Correlation across broker; metrics + alerts |
| IX — Security (`SEC`) | ☐ | AuthZ on resource; no card data; no PII leakage |
| X — Quality gates (`QAG`) | ☐ | TDD; coverage; idempotency + concurrency tests |

**Deviations requested**: None \| [rule ID] — see waiver [ID] in `constitution.md` § XI.5

---

## 14. Test Plan

<!-- QAG-001: tests are written first and MUST be observed failing before implementation. -->

| Level | Scope | Infrastructure | Rule |
|---|---|---|---|
| **Unit — Domain** | Invariants, state machine, value objects | None *(a DB here means an ARC-004 violation)* | QAG-003 |
| **Unit — Application** | Handler orchestration, authorization | Mocked ports | |
| **Integration — Persistence** | Repository, migrations, concurrency | **Testcontainers PostgreSQL** | QAG-008 |
| **Integration — Messaging** | Outbox atomicity, inbox dedup, consumer | **Testcontainers broker** | QAG-008 |
| **Contract** | Producer emits per schema; consumers tolerate | Golden samples | QAG-007, EVG-052 |
| **Concurrency** | [N] parallel [operation] → exactly [M] succeed | Real DB | QAG-006 |
| **Saga** | Happy path + **every** compensation branch | Real broker | QAG-004 |
| **Load** | [target] req/s | Staging | QAG-009 |

**Coverage target**: 90% *(money/inventory)* \| 80% *(other)* — **MUST** gate CI *(QAG-002)*

---

## 15. Rollout

| | |
|---|---|
| **Feature flag** | `[flag-name]` \| N/A *(required for risky features — QAG-011)* |
| **Deployment strategy** | Blue-green \| Canary *(required for checkout/payment — QAG-011)* \| Standard |
| **Contract dual-publish** | N/A \| [event] v1+v2 for [≥30 days — EVG-036] |
| **Consumer migration** | [which contexts must redeploy, in what order] |
| **Rollback** | [procedure; note anything irreversible] |
| **Monitoring window** | [duration + what is watched before the flag is removed] |

---

## 16. Open Questions

| # | Question | Blocks | Owner | Needed by |
|---|---|---|---|---|
| 1 | [question] | [which section] | [name] | [date] |

<!-- A spec MUST NOT reach "Approved" with an open question that blocks §§ 4–9.
     Questions blocking only §15 may be resolved during implementation. -->

---

## 17. References

- Constitution: [`specs/constitution.md`](../constitution.md) — rules cited above
- Context map: [`specs/contexts.md`](../contexts.md) § [N]
- Event governance: [`specs/event-governance.md`](../event-governance.md)
- Dev workflow: [`specs/guidelines.md`](../guidelines.md)
- ADRs: [links]
- Related specs: [links]

---

## 18. Definition of Done

- [ ] Every section above completed; no guidance comments or unresolved placeholders remain
- [ ] Owning context maintainer approved
- [ ] Every consuming context maintainer approved (COM-041)
- [ ] Constitution Check (§ 13) has no unwaived deviation
- [ ] Contracts committed under `contracts/` and CI compatibility check passes (COM-042, EVG-050)
- [ ] Event catalog updated (EVG-054)
- [ ] Acceptance criteria are executable and map 1:1 to named tests
- [ ] Tests written and observed failing before implementation (QAG-001)
- [ ] Coverage gate met (QAG-002)
- [ ] Metrics, alerts, and runbook links in place (OBS-031, OBS-032)
