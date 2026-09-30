using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using MigrateFromSqlToCosmosDb.Application.Abstractions;
using MigrateFromSqlToCosmosDb.Domain.Trading;

namespace MigrateFromSqlToCosmosDb.Infrastructure.SqlServer;

public sealed class SqlTradeSourceReader(IOptions<SqlServerOptions> options) : ITradeSourceReader
{
    private readonly SqlServerOptions options = options.Value;

    public async Task<IReadOnlyCollection<Trade>> GetInitialBatchAsync(long? afterId, int batchSize, CancellationToken cancellationToken)
    {
        const string sql = "SELECT TOP (@batchSize) Id, AccountId, Instrument, Quantity, Price, ExecutedAt, Status FROM dbo.Trades WHERE Id > @afterId ORDER BY Id";
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        command.Parameters.Add(new SqlParameter("@batchSize", SqlDbType.Int) { Value = batchSize });
        command.Parameters.Add(new SqlParameter("@afterId", SqlDbType.BigInt) { Value = afterId ?? 0L });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await ReadTradesAsync(reader, cancellationToken);
    }

    public async Task<IReadOnlyCollection<TradeChange>> GetChangesAsync(byte[] fromLsn, byte[] toLsn, CancellationToken cancellationToken)
    {
        const string sql = "SELECT __$start_lsn, __$operation, Id, AccountId, Instrument, Quantity, Price, ExecutedAt, Status FROM cdc.fn_cdc_get_all_changes_dbo_Trades(@fromLsn, @toLsn, N'all') WHERE __$operation IN (1, 2, 4) ORDER BY __$start_lsn, __$seqval";
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        command.Parameters.Add(new SqlParameter("@fromLsn", SqlDbType.Binary, 10) { Value = fromLsn });
        command.Parameters.Add(new SqlParameter("@toLsn", SqlDbType.Binary, 10) { Value = toLsn });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var changes = new List<TradeChange>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var lsn = reader.GetFieldValue<byte[]>(0);
            var operation = reader.GetByte(1);
            var trade = new Trade(reader.GetInt64(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8));
            changes.Add(new TradeChange(trade, lsn, operation == 1 ? ChangeOperation.Delete : ChangeOperation.Upsert));
        }

        return changes;
    }

    public async Task<byte[]> GetMaximumLsnAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, "SELECT sys.fn_cdc_get_max_lsn()");
        return (byte[])(await command.ExecuteScalarAsync(cancellationToken) ?? throw new InvalidOperationException("CDC returned no maximum LSN."));
    }

    private SqlConnection CreateConnection() => new(options.ConnectionString);

    private SqlCommand CreateCommand(SqlConnection connection, string commandText) => new(commandText, connection)
    {
        CommandTimeout = options.CommandTimeoutSeconds
    };

    private static async Task<IReadOnlyCollection<Trade>> ReadTradesAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        var trades = new List<Trade>();
        while (await reader.ReadAsync(cancellationToken))
        {
            trades.Add(new Trade(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetFieldValue<DateTimeOffset>(5), reader.GetString(6)));
        }

        return trades;
    }
}
