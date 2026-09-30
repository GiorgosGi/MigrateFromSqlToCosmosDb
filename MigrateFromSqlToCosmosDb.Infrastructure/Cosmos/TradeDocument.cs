namespace MigrateFromSqlToCosmosDb.Infrastructure.Cosmos;

public sealed class TradeDocument
{
    public required string Id { get; init; }
    public required string AccountId { get; init; }
    public long SourceId { get; init; }
    public required string Instrument { get; init; }
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public DateTimeOffset ExecutedAt { get; init; }
    public required string Status { get; init; }
    public required string SourceLsn { get; init; }
    public bool IsDeleted { get; init; }
}
