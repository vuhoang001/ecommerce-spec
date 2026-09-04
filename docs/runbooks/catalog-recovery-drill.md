# Catalog Recovery Drill

**Satisfies**: `SC-015` (99.9% monthly availability), `SC-016` (service restored within 15
minutes, **demonstrated by a recovery exercise rather than asserted**).

`SC-016` is met by a recorded drill result, not by a configuration value — which is why this file
has a results table and not just a procedure (research.md R14).

## Preconditions

- Two or more host instances behind a load balancer (`FR-036`)
- Managed PostgreSQL with automated failover, or a rehearsed restore inside 15 minutes
  (plan.md Complexity Tracking — this is a deployment dependency the feature does not deliver)
- A load generator producing steady traffic during each exercise

## Exercise 1 — single instance failure

1. Start steady traffic against the load balancer.
2. `kill -9` one host instance.
3. Record failed requests and time to full capacity.

**Pass**: zero failed requests. `FR-036` says no read path depends on one instance being alive.

## Exercise 2 — total outage

1. Stop every host instance.
2. Start them again.
3. Record time from the last instance stopping to the first `/health/ready` returning 200.

**Pass**: under 15 minutes (`SC-016`). Note that start-up seeds the discount copy under an
advisory lock, so only one instance seeds even when all start together (`FR-031`).

## Exercise 3 — database failover

1. Trigger failover on the PostgreSQL primary.
2. Record time until `/health/ready` returns 200 again.

**Pass**: under 15 minutes (`SC-016`). Readiness covers the database and migrations only.

## Exercise 4 — Promotion outage (the one that is easy to get wrong)

1. Make the Promotion module unreachable.
2. Request listings and detail views throughout.

**Pass**: every page still renders, prices come from the discount copy marked possibly out of
date, and **`/health/ready` keeps returning 200 on every instance**. If readiness ever starts
checking Promotion, the load balancer drains every instance and a degraded dependency becomes a
total outage — exactly what `SC-008` forbids (research.md R13).

## Results

| Date | Exercise | Measured | Pass | Operator | Notes |
|---|---|---|---|---|---|
| 2026-09-04 | 4 — Promotion outage | n/a | ✅ | automated | Covered by `HealthProbeTests` and the `PromotionUnavailable`/`ProjectionStaleness` contract tests: readiness stays 200 and every price path still resolves. |
| _pending_ | 1 — single instance | — | ☐ | — | Requires a deployed load-balanced environment. |
| _pending_ | 2 — total outage | — | ☐ | — | Requires a deployed environment. |
| _pending_ | 3 — database failover | — | ☐ | — | Requires managed PostgreSQL with failover. |

**Status**: Exercise 4 is demonstrated automatically in CI. Exercises 1–3 need a deployed,
load-balanced environment with managed PostgreSQL and **have not been run**. `SC-015` and
`SC-016` are therefore not yet evidenced.
