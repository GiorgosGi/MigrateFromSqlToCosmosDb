namespace MigrateFromSqlToCosmosDb.Infrastructure.SqlServer;

public sealed class SqlServerOptions
{
    public const string SectionName = "SqlServer";

    public required string ConnectionString { get; init; }
    public int CommandTimeoutSeconds { get; init; } = 30;
    public int BatchSize { get; init; } = 500;
}
