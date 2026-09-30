# Incremental rollout plan

## Phase 1: Foundation
- Provision Cosmos resources from `infra/main.bicep` in a non-production Azure environment.
- Deploy the migration worker only.
- Validate SQL CDC health (capture + cleanup jobs) and worker connectivity/authentication.

## Phase 2: Initial backfill
- Run worker with backfill enabled until `trade-backfill` checkpoint is complete.
- Verify source-to-Cosmos counts by account and date range.
- Review RU consumption, latency, and throttling (429) behavior.

## Phase 3: Continuous CDC
- Keep worker running until CDC lag and checkpoint age stabilize within SLA.
- Track synchronization metrics (`sync.processed_changes`, failed runs, run duration).
- Execute replay drill by rewinding checkpoint in lower environment.

## Phase 4: Shadow-read validation
- Keep current SQL consumers unchanged.
- Compare SQL query outputs with API outputs for sampled accounts, instruments, and date ranges.
- Validate pagination token round trips and ordering consistency.

## Phase 5: Canary cutover
- Redirect a small subset of consumers to `/api/v1/trades`.
- Monitor auth failures, P95/P99 latency, and correctness deltas.
- Expand traffic gradually when metrics remain stable.

## Rollback
- If correctness/performance regressions occur, route consumers back to SQL read paths.
- Keep worker running so Cosmos remains synchronized.
- Fix issues, replay/repair data if needed, then retry canary rollout.
