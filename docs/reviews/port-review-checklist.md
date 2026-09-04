# Port Review Checklist

**Rules enforced here**: `COM-002`, `COM-003` — both are enforced by *code review of every port
implementation*, per the constitution. This file is the artifact that review produces, so
`GATE-001` has something to cite.

Complete one block per port implementation, in the pull request that introduces or changes it.

## How to use

For each type implementing a port interface (`I*Port` declared in a module's `Application`
assembly), a reviewer confirms every line below and records the outcome.

- [ ] **COM-002 — synchronous call depth is 1.** While serving this call, the implementation
      makes no further cross-module call. Trace every method it invokes; a call into another
      module's port, adapter, or generated client is a violation.
- [ ] **COM-003 — no enlistment in the caller's transaction.** The implementation opens no
      `DbTransaction` belonging to the caller, receives no `DbContext` from the caller, and does
      not run inside an ambient transaction. The caller has committed, or has not yet opened one.
- [ ] **Failure is a value, not an exception.** The port returns a discriminated result so an
      unreachable provider is a state the caller handles (FR-013), not a stack unwind.
- [ ] **No write path.** The interface exposes no operation that changes the provider's state
      (PRM-001 [withdrawn citation], verified mechanically by `Prm001NoDiscountCalculationTests`).

## Reviewed implementations

| Implementation | Reviewer | Date | COM-002 | COM-003 | Notes |
|---|---|---|---|---|---|
| `InProcessPromotionPricingAdapter` | _pending_ | _pending_ | ☐ | ☐ | In-process today; a `GrpcChannel` client replaces it after extraction (research.md R5). Both must be reviewed against this list. |

## Why this is a document and not a test

The constitution names review as the enforcement mechanism for `COM-002` and `COM-003`, because
call depth and transaction ambience are properties of a call graph that an assembly-level
architecture test cannot see. `GATE-001` allows this: a rule must be checkable by a test, a gate,
**or a review checklist item**. This is that item.
