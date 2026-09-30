using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using MigrateFromSqlToCosmosDb.Application.Abstractions;
using MigrateFromSqlToCosmosDb.Domain.Trading;

namespace MigrateFromSqlToCosmosDb.Infrastructure.Cosmos;

public sealed class CosmosTradeStore(Container container) : ITradeReadStore, ITradeDocumentWriter
{
    public async Task<TradePage> GetPageAsync(TradeQuery query, CancellationToken cancellationToken)
    {
        var definition = new QueryDefinition("SELECT * FROM c WHERE c.accountId = @accountId AND c.isDeleted = false AND (@instrument = null OR c.instrument = @instrument) AND (@from = null OR c.executedAt >= @from) AND (@to = null OR c.executedAt <= @to) ORDER BY c.executedAt DESC, c.id ASC")
            .WithParameter("@accountId", query.AccountId)
            .WithParameter("@instrument", query.Instrument)
            .WithParameter("@from", query.From)
            .WithParameter("@to", query.To);

        var tokenState = DecodeAndValidateContinuationToken(query);

        using var iterator = container.GetItemQueryIterator<TradeDocument>(
            definition,
            tokenState?.CosmosToken,
            new QueryRequestOptions { PartitionKey = new PartitionKey(query.AccountId), MaxItemCount = query.PageSize });
        var response = await iterator.ReadNextAsync(cancellationToken);

        return new TradePage(
            response.Select(document => new Trade(document.SourceId, document.AccountId, document.Instrument, document.Quantity, document.Price, document.ExecutedAt, document.Status)).ToArray(),
            EncodeContinuationToken(response.ContinuationToken, query));
    }

    public async Task UpsertAsync(TradeChange change, CancellationToken cancellationToken)
    {
        var document = new TradeDocument
        {
            Id = $"trade:{change.Trade.Id}",
            AccountId = change.Trade.AccountId,
            SourceId = change.Trade.Id,
            Instrument = change.Trade.Instrument,
            Quantity = change.Trade.Quantity,
            Price = change.Trade.Price,
            ExecutedAt = change.Trade.ExecutedAt,
            Status = change.Trade.Status,
            SourceLsn = Convert.ToHexString(change.Lsn),
            IsDeleted = change.Operation == ChangeOperation.Delete
        };

        try
        {
            var current = await container.ReadItemAsync<TradeDocument>(document.Id, new PartitionKey(document.AccountId), cancellationToken: cancellationToken);
            if (string.CompareOrdinal(current.Resource.SourceLsn, document.SourceLsn) >= 0)
            {
                return;
            }
        }
        catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
        }

        await container.UpsertItemAsync(document, new PartitionKey(document.AccountId), cancellationToken: cancellationToken);
    }

    private static string? EncodeContinuationToken(string? cosmosToken, TradeQuery query)
    {
        if (string.IsNullOrWhiteSpace(cosmosToken))
        {
            return null;
        }

        var envelope = new ContinuationEnvelope(cosmosToken, BuildQueryHash(query));
        return ToBase64Url(JsonSerializer.Serialize(envelope));
    }

    private static ContinuationEnvelope? DecodeAndValidateContinuationToken(TradeQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.ContinuationToken))
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<ContinuationEnvelope>(FromBase64Url(query.ContinuationToken))
                ?? throw new InvalidOperationException("Invalid continuation token.");
            if (!string.Equals(envelope.QueryHash, BuildQueryHash(query), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The continuation token does not match the active filter set.");
            }

            return envelope;
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Invalid continuation token format.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Invalid continuation token payload.");
        }
    }

    private static string BuildQueryHash(TradeQuery query)
    {
        var fingerprint = $"v1|{query.AccountId}|{query.From:O}|{query.To:O}|{query.Instrument?.Trim().ToUpperInvariant()}|order:executedAt-desc,id-asc";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
    }

    private static string ToBase64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
    }

    private sealed record ContinuationEnvelope(string CosmosToken, string QueryHash);
}
