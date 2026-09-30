using MigrateFromSqlToCosmosDb.Domain.Trading;

namespace MigrateFromSqlToCosmosDb.Application.Abstractions;

public interface ITradeReadStore
{
    Task<TradePage> GetPageAsync(TradeQuery query, CancellationToken cancellationToken);
}

public sealed record TradeQuery(
    string AccountId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Instrument,
    int PageSize,
    string? ContinuationToken);

public sealed record TradePage(IReadOnlyCollection<Trade> Items, string? ContinuationToken);
