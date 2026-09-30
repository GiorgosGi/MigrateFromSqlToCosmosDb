using MigrateFromSqlToCosmosDb.Application.Abstractions;

namespace MigrateFromSqlToCosmosDb.Application.Synchronization;

public sealed class TradeSynchronizationService(
    ITradeSourceReader sourceReader,
    ITradeDocumentWriter documentWriter,
    ISynchronizationCheckpointStore checkpoints)
{
    public Task InitializeAsync(byte[] initialLsn, CancellationToken cancellationToken) => checkpoints.SaveAsync(initialLsn, cancellationToken);

    public async Task<int> SynchronizeAsync(CancellationToken cancellationToken)
    {
        var fromLsn = await checkpoints.GetAsync(cancellationToken);
        var toLsn = await sourceReader.GetMaximumLsnAsync(cancellationToken);

        if (fromLsn is null)
        {
            throw new InvalidOperationException("CDC synchronization must be initialized from the completed backfill high-water LSN.");
        }

        var changes = await sourceReader.GetChangesAsync(fromLsn, toLsn, cancellationToken);
        foreach (var change in changes.OrderBy(change => Convert.ToHexString(change.Lsn)))
        {
            await documentWriter.UpsertAsync(change, cancellationToken);
        }

        await checkpoints.SaveAsync(toLsn, cancellationToken);
        return changes.Count;
    }
}
