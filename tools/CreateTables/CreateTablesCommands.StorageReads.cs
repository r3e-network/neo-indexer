using Npgsql;

internal static partial class CreateTablesCommands
{
    internal static async Task PruneStorageReadsAsync(NpgsqlConnection connection, string[] args)
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
}
