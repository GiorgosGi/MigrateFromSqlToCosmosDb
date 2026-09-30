namespace MigrateFromSqlToCosmosDb.Domain.Trading;

public sealed record Trade(
    long Id,
    string AccountId,
    string Instrument,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt,
    string Status);
