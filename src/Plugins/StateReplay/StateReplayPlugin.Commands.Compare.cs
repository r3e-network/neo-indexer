// Copyright (C) 2015-2025 The Neo Project.
//
// StateReplayPlugin.Commands.Compare.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.ConsoleService;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace StateReplay
{
    public partial class StateReplayPlugin
    {
        [ConsoleCommand("replay compare", Category = "Replay", Description = "Compare replay execution with live execution and generate diff report")]
        internal void CompareBlockExecution(string filePath)
        {
            if (_system is null)
                throw new InvalidOperationException("NeoSystem is not ready.");

            // Load state file
            BinaryStateFile? binaryFile = null;
            Dictionary<string, byte[]>? jsonEntries = null;
            uint blockIndex;

            if (BinaryFormatReader.IsBinaryFormat(filePath))
            {
                binaryFile = BinaryFormatReader.Read(filePath);
                blockIndex = binaryFile.BlockIndex;
            }
            else
            {
                // JSON format
                var json = JsonDocument.Parse(File.ReadAllBytes(filePath));
                blockIndex = json.RootElement.GetProperty("block").GetUInt32();
                jsonEntries = new Dictionary<string, byte[]>();
                foreach (var entry in json.RootElement.GetProperty("keys").EnumerateArray())
                {
                    var key = entry.GetProperty("key").GetString()!;
                    var value = Convert.FromBase64String(entry.GetProperty("value").GetString()!);
                    jsonEntries[key] = value;
                }
            }

            var block = NativeContract.Ledger.GetBlock(_system.StoreView, blockIndex);
            if (block is null)
            {
                ConsoleHelper.Error($"Block {blockIndex} not found on this node.");
                return;
            }

            // For comparison, we'd need to intercept reads during both executions.
            // This is a simplified implementation that summarizes the loaded state.
            var report = new StringBuilder();
            report.AppendLine($"=== Block {blockIndex} Comparison Report ===");
            report.AppendLine($"Block Hash: {block.Hash}");
            report.AppendLine($"Transactions: {block.Transactions.Length}");
            report.AppendLine();

            if (binaryFile != null)
            {
                report.AppendLine($"Snapshot Entries: {binaryFile.Entries.Count}");
                // Group by contract
                var byContract = binaryFile.Entries.GroupBy(e => e.ContractHash.ToString());
                foreach (var group in byContract.OrderBy(g => g.Key))
                {
                    report.AppendLine($"  Contract {group.Key}: {group.Count()} reads");
                }
            }
            else if (jsonEntries != null)
            {
                report.AppendLine($"Snapshot Entries: {jsonEntries.Count}");
            }

            report.AppendLine();
            report.AppendLine("Comparison complete. (Full diff requires live execution capture - not implemented in this version)");

            ConsoleHelper.Info(report.ToString());
        }
    }
}
