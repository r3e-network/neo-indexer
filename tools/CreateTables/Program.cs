using Npgsql;
using System;
using System.Globalization;

const string ConnectionStringEnvVar = "NEO_STATE_RECORDER__SUPABASE_CONNECTION_STRING";

if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help" || args[0] == "help"))
{
    PrintUsage();
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
            await ApplyMigrationsAsync(connection).ConfigureAwait(false);
            await ListPublicTablesAsync(connection).ConfigureAwait(false);
            break;

        case "ensure-trace-partitions":
            await EnsureTracePartitionsAsync(connection, args).ConfigureAwait(false);
            break;

        case "prune-trace-partitions":
            await PruneTracePartitionsAsync(connection, args).ConfigureAwait(false);
            break;

        case "prune-storage-reads":
            await PruneStorageReadsAsync(connection, args).ConfigureAwait(false);
            break;

        case "partition-stats":
            await GetPartitionStatsAsync(connection, args).ConfigureAwait(false);
            break;

        default:
            Console.WriteLine($"Unknown command: {command}");
            PrintUsage();
            break;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

static string? FindMigrationsDirectory()
{
    var envOverride = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__MIGRATIONS_DIR");
    if (!string.IsNullOrWhiteSpace(envOverride) && Directory.Exists(envOverride))
        return envOverride;

    var current = Directory.GetCurrentDirectory();
    for (var i = 0; i < 6; i++)
    {
        var candidate = Path.Combine(current, "migrations");
        if (Directory.Exists(candidate))
            return candidate;

        var parent = Directory.GetParent(current);
        if (parent is null)
            break;

        current = parent.FullName;
    }

    return null;
}

static void PrintUsage()
{
    Console.WriteLine("CreateTables (Supabase Admin)");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -c Release --project tools/CreateTables [command] [args]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  migrate                         Apply all SQL files under ./migrations (default).");
    Console.WriteLine("  ensure-trace-partitions <partition_size> <lookahead_blocks>");
    Console.WriteLine("                                  Create missing trace partitions (see migrations/008).");
    Console.WriteLine("  prune-trace-partitions <retention_blocks>");
    Console.WriteLine("                                  Drop old trace partitions and return counts.");
    Console.WriteLine("  prune-storage-reads <retention_blocks> [batch_size] [max_batches]");
    Console.WriteLine("                                  Delete old rows from storage_reads (see migrations/010, 011).");
    Console.WriteLine("  partition-stats <table_name>    List partition row/size stats for a trace table.");
    Console.WriteLine();
    Console.WriteLine("Environment:");
    Console.WriteLine("  NEO_STATE_RECORDER__SUPABASE_CONNECTION_STRING   Direct Postgres connection string (required).");
    Console.WriteLine("  NEO_STATE_RECORDER__MIGRATIONS_DIR               Optional override for migrations directory.");
}

static async Task ApplyMigrationsAsync(NpgsqlConnection connection)
{
    var migrationsDir = FindMigrationsDirectory();
    if (migrationsDir is null)
    {
        Console.WriteLine("Could not locate a 'migrations' directory. Run from the repo root or set NEO_STATE_RECORDER__MIGRATIONS_DIR.");
        return;
    }

    var sqlFiles = Directory.GetFiles(migrationsDir, "*.sql")
        .OrderBy(Path.GetFileName)
        .ToArray();

    if (sqlFiles.Length == 0)
    {
        Console.WriteLine($"No migration files found in {migrationsDir}");
        return;
    }

    foreach (var file in sqlFiles)
    {
        Console.WriteLine($"Applying {Path.GetFileName(file)}...");
        var sql = await File.ReadAllTextAsync(file).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(sql))
        {
            Console.WriteLine("  (skipped empty migration)");
            continue;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}

static async Task ListPublicTablesAsync(NpgsqlConnection connection)
{
    await using var listCommand = connection.CreateCommand();
    listCommand.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;";
    await using var reader = await listCommand.ExecuteReaderAsync().ConfigureAwait(false);
    Console.WriteLine("\nTables:");
    while (await reader.ReadAsync().ConfigureAwait(false))
        Console.WriteLine($"  - {reader.GetString(0)}");
}

static int ParsePositiveInt(string raw, string name)
{
    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        throw new ArgumentException($"{name} must be a positive integer.");
    return parsed;
}

static int ParseNonNegativeInt(string raw, string name)
{
    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        throw new ArgumentException($"{name} must be a non-negative integer.");
    return parsed;
}

static async Task EnsureTracePartitionsAsync(NpgsqlConnection connection, string[] args)
{
    if (args.Length < 3)
        throw new ArgumentException("ensure-trace-partitions requires <partition_size> <lookahead_blocks>.");

    var partitionSize = ParsePositiveInt(args[1], "partition_size");
    var lookaheadBlocks = ParsePositiveInt(args[2], "lookahead_blocks");

    Console.WriteLine($"Ensuring trace partitions (partition_size={partitionSize}, lookahead_blocks={lookaheadBlocks})...");
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT ensure_trace_partitions(@partition_size, @lookahead_blocks);";
    command.Parameters.AddWithValue("partition_size", partitionSize);
    command.Parameters.AddWithValue("lookahead_blocks", lookaheadBlocks);
    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    Console.WriteLine("OK");
}

static async Task PruneTracePartitionsAsync(NpgsqlConnection connection, string[] args)
{
    if (args.Length < 2)
        throw new ArgumentException("prune-trace-partitions requires <retention_blocks>.");

    var retentionBlocks = ParsePositiveInt(args[1], "retention_blocks");

    Console.WriteLine($"Pruning trace partitions (retention_blocks={retentionBlocks})...");
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT table_name, dropped_partitions FROM prune_trace_partitions(@retention_blocks);";
    command.Parameters.AddWithValue("retention_blocks", retentionBlocks);

    await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
    Console.WriteLine("Dropped partitions:");
    while (await reader.ReadAsync().ConfigureAwait(false))
    {
        var tableName = reader.GetString(0);
        var dropped = reader.GetInt32(1);
        Console.WriteLine($"  - {tableName}: {dropped}");
    }
}

static async Task PruneStorageReadsAsync(NpgsqlConnection connection, string[] args)
{
    if (args.Length < 2)
        throw new ArgumentException("prune-storage-reads requires <retention_blocks> [batch_size] [max_batches].");
    if (args.Length > 4)
        throw new ArgumentException("prune-storage-reads takes at most 3 args: <retention_blocks> [batch_size] [max_batches].");

    var retentionBlocks = ParsePositiveInt(args[1], "retention_blocks");
    var batchSize = args.Length >= 3 ? ParsePositiveInt(args[2], "batch_size") : 50000;
    var maxBatches = args.Length >= 4 ? ParseNonNegativeInt(args[3], "max_batches") : 0;

    Console.WriteLine($"Pruning storage_reads (retention_blocks={retentionBlocks}, batch_size={batchSize}, max_batches={maxBatches})...");
    if (maxBatches == 0)
        Console.WriteLine("  Note: max_batches=0 runs until complete.");

    try
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT prune_storage_reads(@retention_blocks, @batch_size, @max_batches);";
        command.Parameters.AddWithValue("retention_blocks", retentionBlocks);
        command.Parameters.AddWithValue("batch_size", batchSize);
        command.Parameters.AddWithValue("max_batches", maxBatches);
        var deleted = (long)(await command.ExecuteScalarAsync().ConfigureAwait(false) ?? 0L);
        Console.WriteLine($"Deleted rows: {deleted}");
    }
    catch (PostgresException ex) when (ex.SqlState == "42883")
    {
        if (args.Length >= 3)
        {
            Console.WriteLine("  Note: batched pruning is unavailable (missing migration 011). Falling back to prune_storage_reads(retention_blocks) and ignoring batch args.");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT prune_storage_reads(@retention_blocks);";
        command.Parameters.AddWithValue("retention_blocks", retentionBlocks);
        var deleted = (long)(await command.ExecuteScalarAsync().ConfigureAwait(false) ?? 0L);
        Console.WriteLine($"Deleted rows: {deleted}");
    }
}

static async Task GetPartitionStatsAsync(NpgsqlConnection connection, string[] args)
{
    if (args.Length < 2)
        throw new ArgumentException("partition-stats requires <table_name>.");

    var tableName = args[1];
    Console.WriteLine($"Partition stats for {tableName}:");

    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT partition_name, row_count, size_bytes FROM get_partition_stats(@table_name);";
    command.Parameters.AddWithValue("table_name", tableName);

    await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
    while (await reader.ReadAsync().ConfigureAwait(false))
    {
        var partitionName = reader.GetString(0);
        var rowCount = reader.GetInt64(1);
        var sizeBytes = reader.GetInt64(2);
        Console.WriteLine($"  - {partitionName}: rows={rowCount}, bytes={sizeBytes}");
    }
}
