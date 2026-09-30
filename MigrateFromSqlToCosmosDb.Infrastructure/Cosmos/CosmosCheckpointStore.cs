using Microsoft.Azure.Cosmos;
using MigrateFromSqlToCosmosDb.Application.Abstractions;

namespace MigrateFromSqlToCosmosDb.Infrastructure.Cosmos;

public sealed class CosmosCheckpointStore(Container container) : ISynchronizationCheckpointStore, IBackfillCheckpointStore
{
    private const string PartitionKey = "trade-sync";
    private const string CdcCheckpointId = "trade-cdc";
    private const string BackfillCheckpointId = "trade-backfill";

    public async Task<byte[]?> GetAsync(CancellationToken cancellationToken)
    {
        var document = await ReadAsync(CdcCheckpointId, cancellationToken);
        return document?.Lsn is null ? null : Convert.FromHexString(document.Lsn);
    }

    public async Task SaveAsync(byte[] lsn, CancellationToken cancellationToken) =>
        await container.UpsertItemAsync(new CheckpointDocument(CdcCheckpointId, PartitionKey, Convert.ToHexString(lsn), null, false), new PartitionKey(PartitionKey), cancellationToken: cancellationToken);

    async Task<BackfillCheckpoint?> IBackfillCheckpointStore.GetAsync(CancellationToken cancellationToken)
    {
        var document = await ReadAsync(BackfillCheckpointId, cancellationToken);
        return document?.Lsn is null ? null : new BackfillCheckpoint(document.LastSourceId, Convert.FromHexString(document.Lsn), document.IsComplete);
    }

    async Task IBackfillCheckpointStore.SaveAsync(BackfillCheckpoint checkpoint, CancellationToken cancellationToken) =>
        await container.UpsertItemAsync(new CheckpointDocument(BackfillCheckpointId, PartitionKey, Convert.ToHexString(checkpoint.HighWaterLsn), checkpoint.LastSourceId, checkpoint.IsComplete), new PartitionKey(PartitionKey), cancellationToken: cancellationToken);

    private async Task<CheckpointDocument?> ReadAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            return (await container.ReadItemAsync<CheckpointDocument>(id, new PartitionKey(PartitionKey), cancellationToken: cancellationToken)).Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private sealed record CheckpointDocument(string Id, string PartitionKey, string? Lsn, long? LastSourceId, bool IsComplete);
}