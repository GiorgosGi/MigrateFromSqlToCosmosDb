using MigrateFromSqlToCosmosDb.Application.Abstractions;
using MigrateFromSqlToCosmosDb.Application.Synchronization;
using MigrateFromSqlToCosmosDb.Domain.Trading;

namespace MigrateFromSqlToCosmosDb.Application.Tests;

public sealed class SynchronizationFlowTests
{
    [Fact]
    public async Task Backfill_WritesAllRowsAndCompletesCheckpoint()
    {
        var source = new FakeSourceReader(
            batches:
            [
                [new Trade(1, "acc-1", "AAPL", 1m, 100m, DateTimeOffset.UtcNow, "Done")],
                [new Trade(2, "acc-1", "MSFT", 2m, 200m, DateTimeOffset.UtcNow, "Done")]
            ],
            maxLsn: [0x00, 0x01]);
        var writer = new FakeWriter();
        var checkpoints = new InMemoryBackfillCheckpointStore();
        var service = new TradeBackfillService(source, writer, checkpoints);

        var written = await service.RunAsync(100, CancellationToken.None);

        Assert.Equal(2, written);
        Assert.Equal(2, writer.Changes.Count);
        Assert.True(checkpoints.Current?.IsComplete);
        Assert.Equal(2, checkpoints.Current?.LastSourceId);
    }

    [Fact]
    public async Task Synchronize_ThrowsWhenNotInitialized()
    {
        var service = new TradeSynchronizationService(
            new FakeSourceReader([], [0x00, 0x02]),
            new FakeWriter(),
            new InMemorySyncCheckpointStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SynchronizeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Orchestrator_InitializesCdcFromBackfillHighWater()
    {
        var backfillCheckpointStore = new InMemoryBackfillCheckpointStore
        {
            Current = new BackfillCheckpoint(10, [0x00, 0x09], true)
        };

        var syncCheckpointStore = new InMemorySyncCheckpointStore();
        var source = new FakeSourceReader([], [0x00, 0x0A]);
        source.Changes.Add(new TradeChange(new Trade(10, "acc-1", "AAPL", 1m, 100m, DateTimeOffset.UtcNow, "Done"), [0x00, 0x0A], ChangeOperation.Upsert));
        var writer = new FakeWriter();

        var orchestrator = new TradeMigrationOrchestrator(
            new TradeBackfillService(source, writer, backfillCheckpointStore),
            new TradeSynchronizationService(source, writer, syncCheckpointStore),
            backfillCheckpointStore,
            syncCheckpointStore);

        var processed = await orchestrator.RunAsync(100, CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(1, writer.Changes.Count);
        Assert.Equal("000A", Convert.ToHexString(syncCheckpointStore.Current ?? []));
    }

    private sealed class FakeSourceReader(IReadOnlyList<IReadOnlyCollection<Trade>> batches, byte[] maxLsn) : ITradeSourceReader
    {
        private int index;
        public List<TradeChange> Changes { get; } = [];

        public Task<IReadOnlyCollection<Trade>> GetInitialBatchAsync(long? afterId, int batchSize, CancellationToken cancellationToken)
        {
            if (index >= batches.Count)
            {
                return Task.FromResult<IReadOnlyCollection<Trade>>([]);
            }

            return Task.FromResult(batches[index++]);
        }

        public Task<IReadOnlyCollection<TradeChange>> GetChangesAsync(byte[] fromLsn, byte[] toLsn, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<TradeChange>>(Changes);

        public Task<byte[]> GetMaximumLsnAsync(CancellationToken cancellationToken) => Task.FromResult(maxLsn);
    }

    private sealed class FakeWriter : ITradeDocumentWriter
    {
        public List<TradeChange> Changes { get; } = [];

        public Task UpsertAsync(TradeChange change, CancellationToken cancellationToken)
        {
            Changes.Add(change);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySyncCheckpointStore : ISynchronizationCheckpointStore
    {
        public byte[]? Current { get; set; }

        public Task<byte[]?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

        public Task SaveAsync(byte[] lsn, CancellationToken cancellationToken)
        {
            Current = lsn;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryBackfillCheckpointStore : IBackfillCheckpointStore
    {
        public BackfillCheckpoint? Current { get; set; }

        public Task<BackfillCheckpoint?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

        public Task SaveAsync(BackfillCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            Current = checkpoint;
            return Task.CompletedTask;
        }
    }
}
