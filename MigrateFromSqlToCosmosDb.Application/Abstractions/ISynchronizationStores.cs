using MigrateFromSqlToCosmosDb.Domain.Trading;

namespace MigrateFromSqlToCosmosDb.Application.Abstractions;

public interface ITradeSourceReader
{
    Task<IReadOnlyCollection<Trade>> GetInitialBatchAsync(long? afterId, int batchSize, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TradeChange>> GetChangesAsync(byte[] fromLsn, byte[] toLsn, CancellationToken cancellationToken);
    Task<byte[]> GetMaximumLsnAsync(CancellationToken cancellationToken);
}

public interface ITradeDocumentWriter
{
    Task UpsertAsync(TradeChange change, CancellationToken cancellationToken);
}

public interface ISynchronizationCheckpointStore
{
    Task<byte[]?> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(byte[] lsn, CancellationToken cancellationToken);
}

public interface IBackfillCheckpointStore
{
    Task<BackfillCheckpoint?> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(BackfillCheckpoint checkpoint, CancellationToken cancellationToken);
}

public enum ChangeOperation { Upsert, Delete }

public sealed record TradeChange(Trade Trade, byte[] Lsn, ChangeOperation Operation);

public sealed record BackfillCheckpoint(long? LastSourceId, byte[] HighWaterLsn, bool IsComplete);
