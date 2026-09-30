# MigrateFromSqlToCosmosDb

This solution implements a **SQL Server to Azure Cosmos DB read-model migration flow** for trade data.

## Goal

- Keep SQL Server as system of record.
- Build a Cosmos DB read model optimized for GET APIs.
- Perform:
  1. Initial backfill (snapshot-style ETL).
  2. Continuous sync from SQL Server CDC.
- Expose JWT-protected controller endpoints for reading from Cosmos.

---

## High-level architecture

Projects in solution:

- `MigrateFromSqlToCosmosDb` — ASP.NET Core Web API (controllers)
- `MigrateFromSqlToCosmosDb.MigrationWorker` — background synchronization worker
- `MigrateFromSqlToCosmosDb.Application` — use cases + abstractions
- `MigrateFromSqlToCosmosDb.Domain` — domain records
- `MigrateFromSqlToCosmosDb.Infrastructure` — SQL Server + Cosmos implementations
- `MigrateFromSqlToCosmosDb.Contracts` — API DTOs
- Test projects:
  - `MigrateFromSqlToCosmosDb.Application.Tests`
  - `MigrateFromSqlToCosmosDb.Api.FunctionalTests`

Clean Architecture direction is enforced:

- Domain has no infrastructure dependencies.
- Application depends on Domain/Contracts abstractions.
- Infrastructure implements Application interfaces.
- API and Worker compose via DI.

---

## Data flow

## 1) Initial backfill

`TradeBackfillService`:

1. Reads current max CDC LSN (`high-water LSN`) from SQL Server.
2. Reads source `dbo.Trades` in stable keyset batches (`Id > lastId ORDER BY Id`).
3. Writes each trade to Cosmos as read-model document.
4. Saves backfill checkpoint after each successful batch.
5. Marks backfill complete when source batches are exhausted.

Checkpoint state is persisted in Cosmos checkpoint container.

## 2) CDC continuous synchronization

`TradeSynchronizationService` + worker loop:

1. Starts from persisted CDC checkpoint LSN.
2. Polls SQL CDC (`fn_cdc_get_all_changes_dbo_Trades`).
3. Applies changes to Cosmos in order.
4. Advances checkpoint **only after successful writes**.
5. Retries on failure in next poll cycle.

`TradeMigrationOrchestrator` ensures CDC starts from completed backfill boundary to avoid snapshot-to-CDC gaps.

---

## Cosmos read model

Trade document conventions:

- Deterministic id: `trade:{sourceId}`
- Partition key: `/accountId`
- Delete handling: tombstone (`isDeleted = true`)
- Stale/replay protection: compare source LSN; ignore older/equal events

Continuation token behavior:

- API returns opaque token (base64url envelope).
- Token is bound to query shape (account/date/instrument/sort hash).
- Reusing a token with different filters is rejected.

---

## API flow

Current endpoint:

- `GET /api/v1/trades`

Behavior:

- Requires JWT authentication.
- Requires `TradesRead` policy (`account_id` + scope or role).
- Reads account scope from token claims.
- Supports filter/pagination:
  - `from`, `to`
  - `instrument`
  - `pageSize` (bounded)
  - `continuationToken`

Returns DTO-only response (`PagedResponse<TradeResponse>`), never persistence documents.

---

## Security

- JWT Bearer auth configured from `Jwt` settings.
- Authorization policy requires authenticated user and account scoping claim.
- Managed identity is used for Cosmos client auth (`DefaultAzureCredential`).
- No credentials are committed to source control.

---

## Configuration

API `appsettings.json` includes:

- `Jwt` (Authority, Audience, RequiredScope, RequiredRole)
- `SqlServer` (ConnectionString)
- `Cosmos` (Endpoint, DatabaseName, container names)

Worker uses:

- `Synchronization` (BatchSize, PollIntervalSeconds)
- same `SqlServer`/`Cosmos` sections

---

## Infrastructure and operations

- CDC enablement script: `database/EnableCdc.sql`
- Cosmos IaC: `infra/main.bicep`
- Operations runbook: `docs/Operations.md`
- Rollout plan: `docs/RolloutPlan.md`

Recommended rollout:

1. Deploy Cosmos + worker.
2. Run backfill and reconcile counts.
3. Let CDC stabilize.
4. Shadow-read comparisons.
5. Canary API cutover.
6. Keep rollback path to SQL reads.

---

## Testing and CI

Implemented tests include:

- Application synchronization flow tests.
- API controller behavior tests (validation/auth/token errors).

CI pipeline (`.github/workflows/ci.yml`) runs:

- restore/build/test
- format verification
- Bicep validation
- secret scanning

---

## Current scope note

The implementation currently focuses on an assumed simple `Trades`-centric schema.
Report-specific endpoints/projections can be added using the same backfill + CDC + Cosmos read-model pattern.
