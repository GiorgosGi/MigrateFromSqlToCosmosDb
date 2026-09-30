namespace MigrateFromSqlToCosmosDb.Domain.Reporting;

public sealed record MonthlyReport(
    long Id,
    string AccountId,
    int Year,
    int Month,
    decimal TotalVolume,
    decimal TotalValue,
    DateTimeOffset GeneratedAt);
