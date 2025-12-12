#!/usr/bin/env dotnet-script
#r "nuget: Npgsql, 10.0.0"

using Npgsql;
using System;

var connectionString = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__SUPABASE_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("NEO_STATE_RECORDER__SUPABASE_CONNECTION_STRING is not set. Aborting to avoid using embedded credentials.");
    Environment.Exit(1);
}

Console.WriteLine("Connecting to Supabase PostgreSQL...");
Console.WriteLine($"Connection: {connectionString[..Math.Min(50, connectionString.Length)]}...");

var migrationsDir = Environment.GetEnvironmentVariable("NEO_STATE_RECORDER__MIGRATIONS_DIR")
    ?? Path.Combine(Directory.GetCurrentDirectory(), "migrations");

if (!Directory.Exists(migrationsDir))
{
    Console.WriteLine($"Migrations directory not found: {migrationsDir}");
    Environment.Exit(1);
}

try
{
    await using var dataSource = NpgsqlDataSource.Create(connectionString);
    await using var connection = await dataSource.OpenConnectionAsync();

    Console.WriteLine("Connected successfully!");

    var sqlFiles = Directory.GetFiles(migrationsDir, "*.sql")
        .OrderBy(Path.GetFileName)
        .ToArray();

    foreach (var file in sqlFiles)
    {
        Console.WriteLine($"Applying {Path.GetFileName(file)}...");
        var sql = await File.ReadAllTextAsync(file);
        if (string.IsNullOrWhiteSpace(sql)) continue;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    Console.WriteLine("Migrations applied successfully!");

    // List tables
    await using var listCommand = connection.CreateCommand();
    listCommand.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;";
    await using var reader = await listCommand.ExecuteReaderAsync();

    Console.WriteLine("\nTables in database:");
    while (await reader.ReadAsync())
    {
        Console.WriteLine($"  - {reader.GetString(0)}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
}
