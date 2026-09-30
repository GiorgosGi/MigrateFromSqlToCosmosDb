using MigrateFromSqlToCosmosDb.Application.Abstractions;
using MigrateFromSqlToCosmosDb.Contracts.Trades;

namespace MigrateFromSqlToCosmosDb.Application.Trades;

public sealed class TradeQueryService(ITradeReadStore readStore)
{
    public async Task<PagedResponse<TradeResponse>> GetPageAsync(TradeQuery query, CancellationToken cancellationToken)
    {
        var page = await readStore.GetPageAsync(query, cancellationToken);
        return new PagedResponse<TradeResponse>(
            page.Items.Select(trade => new TradeResponse(
                trade.Id,
                trade.AccountId,
                trade.Instrument,
                trade.Quantity,
                trade.Price,
                trade.ExecutedAt,
                trade.Status)).ToArray(),
            page.ContinuationToken);
    }
}
