using Npgsql;

internal static partial class CreateTablesCommands
{
    internal static async Task ApplyMigrationsAsync(NpgsqlConnection connection)
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

    internal static async Task ListPublicTablesAsync(NpgsqlConnection connection)
    {
        await using var listCommand = connection.CreateCommand();
        listCommand.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;";
        await using var reader = await listCommand.ExecuteReaderAsync().ConfigureAwait(false);
        Console.WriteLine("\nTables:");
        while (await reader.ReadAsync().ConfigureAwait(false))
            Console.WriteLine($"  - {reader.GetString(0)}");
    }
}
