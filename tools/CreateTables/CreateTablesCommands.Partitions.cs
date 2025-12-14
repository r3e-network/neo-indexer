using Npgsql;

internal static partial class CreateTablesCommands
{
    internal static async Task EnsureTracePartitionsAsync(NpgsqlConnection connection, string[] args)
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

    internal static async Task PruneTracePartitionsAsync(NpgsqlConnection connection, string[] args)
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

    internal static async Task GetPartitionStatsAsync(NpgsqlConnection connection, string[] args)
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
}
