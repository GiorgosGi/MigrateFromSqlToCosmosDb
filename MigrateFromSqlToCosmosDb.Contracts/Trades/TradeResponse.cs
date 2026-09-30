namespace MigrateFromSqlToCosmosDb.Contracts.Trades;

public sealed record TradeResponse(
    long Id,
    string AccountId,
    string Instrument,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt,
    string Status);

public sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, string? ContinuationToken);
