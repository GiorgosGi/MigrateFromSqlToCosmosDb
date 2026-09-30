using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MigrateFromSqlToCosmosDb.Application.Abstractions;
using MigrateFromSqlToCosmosDb.Infrastructure.Cosmos;
using MigrateFromSqlToCosmosDb.Infrastructure.SqlServer;

namespace MigrateFromSqlToCosmosDb.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SqlServerOptions>(configuration.GetSection(SqlServerOptions.SectionName));
        services.Configure<CosmosOptions>(configuration.GetSection(CosmosOptions.SectionName));
        var sqlOptions = configuration.GetSection(SqlServerOptions.SectionName).Get<SqlServerOptions>()
            ?? throw new InvalidOperationException("SqlServer configuration is required.");

        services.AddDbContext<TradeSourceDbContext>(options => options.UseSqlServer(sqlOptions.ConnectionString));
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosOptions>>().Value;
            return new CosmosClient(options.Endpoint, new DefaultAzureCredential(), new CosmosClientOptions { AllowBulkExecution = true });
        });

        services.AddScoped<ITradeSourceReader, SqlTradeSourceReader>();
        services.AddSingleton<CosmosTradeStore>(sp =>
        {
            var client = sp.GetRequiredService<CosmosClient>();
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosOptions>>().Value;
            return new CosmosTradeStore(client.GetContainer(options.DatabaseName, options.TradesContainerName));
        });
        services.AddSingleton<ITradeReadStore>(sp => sp.GetRequiredService<CosmosTradeStore>());
        services.AddSingleton<ITradeDocumentWriter>(sp => sp.GetRequiredService<CosmosTradeStore>());
        services.AddSingleton<CosmosCheckpointStore>(sp =>
        {
            var client = sp.GetRequiredService<CosmosClient>();
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosOptions>>().Value;
            return new CosmosCheckpointStore(client.GetContainer(options.DatabaseName, options.CheckpointsContainerName));
        });
        services.AddSingleton<ISynchronizationCheckpointStore>(sp => sp.GetRequiredService<CosmosCheckpointStore>());
        services.AddSingleton<IBackfillCheckpointStore>(sp => sp.GetRequiredService<CosmosCheckpointStore>());

        return services;
    }
}