using Npgsql;
using System;

// Direct connection to Supabase PostgreSQL.
var connectionString = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("NEO_STATE_RECORDER__SUPABASE_CONNECTION_STRING is not set. Aborting to avoid using embedded credentials.");
    return;
}

var migrationsDir = FindMigrationsDirectory();
if (migrationsDir is null)
{
    Console.WriteLine("Could not locate a 'migrations' directory. Run from the repo root or set NEO_STATE_RECORDER__MIGRATIONS_DIR.");
    return;
}

Console.WriteLine("Connecting to Supabase PostgreSQL...");

try
{
    var builder = new NpgsqlDataSourceBuilder(connectionString);
    await using var dataSource = builder.Build();
    await using var connection = await dataSource.OpenConnectionAsync();
    Console.WriteLine("Connected!");

    var sqlFiles = Directory.GetFiles(migrationsDir, "*.sql")
        .OrderBy(Path.GetFileName)
        .ToArray();

    if (sqlFiles.Length == 0)
    {
        Console.WriteLine($"No migration files found in {migrationsDir}");
    }
    else
    {
        foreach (var file in sqlFiles)
        {
            Console.WriteLine($"Applying {Path.GetFileName(file)}...");
            var sql = await File.ReadAllTextAsync(file);
            if (string.IsNullOrWhiteSpace(sql))
            {
                Console.WriteLine("  (skipped empty migration)");
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }

    await using var listCommand = connection.CreateCommand();
    listCommand.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;";
    await using var reader = await listCommand.ExecuteReaderAsync();
    Console.WriteLine("\nTables:");
    while (await reader.ReadAsync())
        Console.WriteLine($"  - {reader.GetString(0)}");
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
