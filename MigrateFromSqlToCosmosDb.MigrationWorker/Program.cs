using MigrateFromSqlToCosmosDb.MigrationWorker;
using MigrateFromSqlToCosmosDb.Application.Synchronization;
using MigrateFromSqlToCosmosDb.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.Configure<SynchronizationOptions>(builder.Configuration.GetSection(SynchronizationOptions.SectionName));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<TradeBackfillService>();
builder.Services.AddScoped<TradeSynchronizationService>();
builder.Services.AddScoped<TradeMigrationOrchestrator>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
