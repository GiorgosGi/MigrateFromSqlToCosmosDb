# Synchronization operations

## Recovery and rebuild

1. Stop the migration worker to prevent concurrent checkpoint updates.
2. Investigate the logged source row, CDC LSN, and Cosmos request diagnostics.
3. To replay CDC, replace the `trade-cdc` checkpoint with an earlier retained LSN in the `synchronization-checkpoints` container, then restart the worker.
4. To rebuild the read model, delete the trade and checkpoint containers, recreate them through `infra/main.bicep`, and restart the worker. It captures a new high-water LSN and resumes the backfill safely.
5. Compare source and Cosmos counts per account and executed-date range before moving consumers to the read API.

## Required alerts

- Worker process unavailable or repeated synchronization failures.
- CDC cleanup retention lower than the maximum outage/replay window.
- Checkpoint age exceeding the freshness objective.
- Cosmos 429 throttling or unexpected RU consumption.
- Reconciliation count mismatch by account/date range.
