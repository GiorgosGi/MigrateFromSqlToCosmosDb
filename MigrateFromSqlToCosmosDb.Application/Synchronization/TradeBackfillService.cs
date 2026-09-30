using MigrateFromSqlToCosmosDb.Application.Abstractions;

namespace MigrateFromSqlToCosmosDb.Application.Synchronization;

public sealed class TradeBackfillService(
    ITradeSourceReader sourceReader,
    ITradeDocumentWriter documentWriter,
    IBackfillCheckpointStore checkpoints)
{
    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        var checkpoint = await checkpoints.GetAsync(cancellationToken);
        if (checkpoint?.IsComplete == true)
        {
            return 0;
        }

        var highWaterLsn = checkpoint?.HighWaterLsn ?? await sourceReader.GetMaximumLsnAsync(cancellationToken);
        var afterId = checkpoint?.LastSourceId;
        var written = 0;

        while (true)
        {
            var batch = await sourceReader.GetInitialBatchAsync(afterId, batchSize, cancellationToken);
            if (batch.Count == 0)
            {
                await checkpoints.SaveAsync(new BackfillCheckpoint(afterId, highWaterLsn, true), cancellationToken);
                return written;
            }

            foreach (var trade in batch)
            {
                await documentWriter.UpsertAsync(new TradeChange(trade, highWaterLsn, ChangeOperation.Upsert), cancellationToken);
            }

            afterId = batch.Max(trade => trade.Id);
            await checkpoints.SaveAsync(new BackfillCheckpoint(afterId, highWaterLsn, false), cancellationToken);
            written += batch.Count;
        }
    }
}