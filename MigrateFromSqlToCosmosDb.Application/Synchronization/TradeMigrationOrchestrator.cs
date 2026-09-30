using MigrateFromSqlToCosmosDb.Application.Abstractions;

namespace MigrateFromSqlToCosmosDb.Application.Synchronization;

public sealed class TradeMigrationOrchestrator(
    TradeBackfillService backfillService,
    TradeSynchronizationService synchronizationService,
    IBackfillCheckpointStore backfillCheckpoints,
    ISynchronizationCheckpointStore synchronizationCheckpoints)
{
    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        var backfilled = await backfillService.RunAsync(batchSize, cancellationToken);
        var backfill = await backfillCheckpoints.GetAsync(cancellationToken)
            ?? throw new InvalidOperationException("The backfill checkpoint was not persisted.");

        if (!backfill.IsComplete)
        {
            return backfilled;
        }

        if (await synchronizationCheckpoints.GetAsync(cancellationToken) is null)
        {
            await synchronizationService.InitializeAsync(backfill.HighWaterLsn, cancellationToken);
        }

        return backfilled + await synchronizationService.SynchronizeAsync(cancellationToken);
    }
}