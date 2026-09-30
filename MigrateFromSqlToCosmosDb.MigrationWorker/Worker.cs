using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using MigrateFromSqlToCosmosDb.Application.Synchronization;

namespace MigrateFromSqlToCosmosDb.MigrationWorker;

public sealed class Worker(
    IServiceScopeFactory scopeFactory,
    IOptions<SynchronizationOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly Meter Meter = new("MigrateFromSqlToCosmosDb.Synchronization", "1.0.0");
    private static readonly Counter<long> ProcessedChangesCounter = Meter.CreateCounter<long>("sync.processed_changes");
    private static readonly Counter<long> FailedRunsCounter = Meter.CreateCounter<long>("sync.failed_runs");
    private static readonly Histogram<double> RunDurationHistogram = Meter.CreateHistogram<double>("sync.run_duration_ms", unit: "ms");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var startedAt = DateTimeOffset.UtcNow;

            try
            {
                using var scope = scopeFactory.CreateScope();
                var migrated = await scope.ServiceProvider.GetRequiredService<TradeMigrationOrchestrator>()
                    .RunAsync(options.Value.BatchSize, stoppingToken);

                ProcessedChangesCounter.Add(migrated);
                logger.LogInformation(
                    "Trade synchronization completed with {ChangeCount} processed source rows/changes and batch size {BatchSize}.",
                    migrated,
                    options.Value.BatchSize);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                FailedRunsCounter.Add(1);
                logger.LogError(exception, "Trade synchronization failed and will be retried.");
            }
            finally
            {
                RunDurationHistogram.Record((DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
        }
    }
}
