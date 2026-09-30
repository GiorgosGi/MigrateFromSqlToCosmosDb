namespace MigrateFromSqlToCosmosDb.MigrationWorker;

public sealed class SynchronizationOptions
{
    public const string SectionName = "Synchronization";

    public int BatchSize { get; init; } = 500;
    public int PollIntervalSeconds { get; init; } = 10;
}