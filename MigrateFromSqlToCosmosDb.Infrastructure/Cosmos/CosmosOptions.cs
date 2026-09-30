namespace MigrateFromSqlToCosmosDb.Infrastructure.Cosmos;

public sealed class CosmosOptions
{
    public const string SectionName = "Cosmos";

    public required string Endpoint { get; init; }
    public required string DatabaseName { get; init; }
    public string TradesContainerName { get; init; } = "trades";
    public string CheckpointsContainerName { get; init; } = "synchronization-checkpoints";
}
