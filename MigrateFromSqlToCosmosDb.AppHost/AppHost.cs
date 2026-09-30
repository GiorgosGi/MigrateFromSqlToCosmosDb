var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MigrateFromSqlToCosmosDb>("migratefromsqltocosmosdb");

builder.Build().Run();
