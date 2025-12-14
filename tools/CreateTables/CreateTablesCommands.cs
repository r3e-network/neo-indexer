using System.Globalization;

internal static partial class CreateTablesCommands
{
    internal static string? FindMigrationsDirectory()
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

    internal static void PrintUsage()
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
        Console.WriteLine("  partition-stats <table_name>    List partition row/size stats for a partitioned table.");
        Console.WriteLine();
        Console.WriteLine("Environment:");
        Console.WriteLine("  NEO_STATE_RECORDER__SUPABASE_CONNECTION_STRING   Direct Postgres connection string (required).");
        Console.WriteLine("  NEO_STATE_RECORDER__MIGRATIONS_DIR               Optional override for migrations directory.");
    }

    private static int ParsePositiveInt(string raw, string name)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
            throw new ArgumentException($"{name} must be a positive integer.");
        return parsed;
    }

    private static int ParseNonNegativeInt(string raw, string name)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            throw new ArgumentException($"{name} must be a non-negative integer.");
        return parsed;
    }
}
