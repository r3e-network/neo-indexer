using Npgsql;
using System;

const string ConnectionStringEnvVar = "NEO_STATE_RECORDER__SUPABASE_CONNECTION_STRING";

if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help" || args[0] == "help"))
{
    CreateTablesCommands.PrintUsage();
    return;
}

var command = args.Length == 0 ? "migrate" : args[0].Trim();

var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine($"{ConnectionStringEnvVar} is not set. Aborting to avoid using embedded credentials.");
    return;
}

Console.WriteLine("Connecting to Supabase PostgreSQL...");

try
{
    var builder = new NpgsqlDataSourceBuilder(connectionString);
    await using var dataSource = builder.Build();
    await using var connection = await dataSource.OpenConnectionAsync();
    Console.WriteLine("Connected!");

    switch (command.ToLowerInvariant())
    {
        case "migrate":
        case "apply":
        case "migrations":
            await CreateTablesCommands.ApplyMigrationsAsync(connection).ConfigureAwait(false);
            await CreateTablesCommands.ListPublicTablesAsync(connection).ConfigureAwait(false);
            break;

        case "ensure-trace-partitions":
            await CreateTablesCommands.EnsureTracePartitionsAsync(connection, args).ConfigureAwait(false);
            break;

        case "prune-trace-partitions":
            await CreateTablesCommands.PruneTracePartitionsAsync(connection, args).ConfigureAwait(false);
            break;

        case "prune-storage-reads":
            await CreateTablesCommands.PruneStorageReadsAsync(connection, args).ConfigureAwait(false);
            break;

        case "partition-stats":
            await CreateTablesCommands.GetPartitionStatsAsync(connection, args).ConfigureAwait(false);
            break;

        default:
            Console.WriteLine($"Unknown command: {command}");
            CreateTablesCommands.PrintUsage();
            break;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
