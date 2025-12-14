// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Endpoints.TraceQuery.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Extensions;
using Neo.Json;
using Neo.Persistence;
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract.Native;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private (uint blockIndex, string blockHash) ResolveBlock(BlockHashOrIndex identifier)
        {
            using var snapshot = system.GetSnapshotCache();
            if (identifier.IsIndex)
            {
                var blockIndex = identifier.AsIndex();
                var hash = NativeContract.Ledger.GetBlockHash(snapshot, blockIndex).NotNull_Or(RpcError.UnknownBlock);
                return (blockIndex, hash.ToString());
            }

            var block = NativeContract.Ledger.GetBlock(snapshot, identifier.AsHash()).NotNull_Or(RpcError.UnknownBlock);
            return (block.Index, block.Hash.ToString());
        }

        private async Task<TraceResult> QueryTraceResultAsync(
            StateRecorderSettings settings,
            uint blockIndex,
            string blockHash,
            string? transactionHashFilter,
            TraceRequestOptions options,
            string? explicitTransactionHash = null)
        {
            var txFilter = transactionHashFilter ?? options.TransactionHash;
            var txResultsQuery = BuildTransactionResultsQuery(blockIndex, txFilter, options);
            var opcodeQuery = BuildTraceQuery(blockIndex, txFilter, options);
            var syscallQuery = BuildTraceQuery(blockIndex, txFilter, options);
            var contractQuery = BuildTraceQuery(blockIndex, txFilter, options);
            var storageReadsQuery = BuildOrderedTraceQuery(blockIndex, txFilter, "read_order", options);
            var storageWritesQuery = BuildOrderedTraceQuery(blockIndex, txFilter, "write_order", options);
            var notificationsQuery = BuildOrderedTraceQuery(blockIndex, txFilter, "notification_order", options);
            var logsQuery = BuildOrderedTraceQuery(blockIndex, txFilter, "log_order", options);

            var txResultsTask = SendSupabaseQueryAsync<TransactionResultResult>(settings, "transaction_results", txResultsQuery);
            var opcodeTask = SendSupabaseQueryAsync<OpCodeTraceResult>(settings, "opcode_traces", opcodeQuery);
            var syscallTask = SendSupabaseQueryAsync<SyscallTraceResult>(settings, "syscall_traces", syscallQuery);
            var contractTask = SendSupabaseQueryAsync<ContractCallResult>(settings, "contract_calls", contractQuery);
            var storageReadsTask = SendSupabaseQueryAsync<StorageReadTraceResult>(settings, "storage_reads", storageReadsQuery);
            var storageWritesTask = SendSupabaseQueryAsync<StorageWriteTraceResult>(settings, "storage_writes", storageWritesQuery);
            var notificationsTask = SendSupabaseQueryAsync<NotificationTraceResult>(settings, "notifications", notificationsQuery);
            var logsTask = SendSupabaseQueryAsync<RuntimeLogTraceResult>(settings, "runtime_logs", logsQuery);

            await Task.WhenAll(txResultsTask, opcodeTask, syscallTask, contractTask, storageReadsTask, storageWritesTask, notificationsTask, logsTask).ConfigureAwait(false);

            var txResultsResponse = await txResultsTask.ConfigureAwait(false);
            var opcodeResponse = await opcodeTask.ConfigureAwait(false);
            var syscallResponse = await syscallTask.ConfigureAwait(false);
            var contractResponse = await contractTask.ConfigureAwait(false);
            var storageReadsResponse = await storageReadsTask.ConfigureAwait(false);
            var storageWritesResponse = await storageWritesTask.ConfigureAwait(false);
            var notificationsResponse = await notificationsTask.ConfigureAwait(false);
            var logsResponse = await logsTask.ConfigureAwait(false);

            return new TraceResult
            {
                BlockIndex = blockIndex,
                BlockHash = blockHash,
                TransactionHash = explicitTransactionHash ?? txFilter,
                Limit = options.Limit,
                Offset = options.Offset,
                TransactionResults = txResultsResponse.Items,
                TransactionResultTotal = txResultsResponse.TotalCount ?? txResultsResponse.Items.Count,
                OpCodeTraces = opcodeResponse.Items,
                OpCodeTotal = opcodeResponse.TotalCount ?? opcodeResponse.Items.Count,
                SyscallTraces = syscallResponse.Items,
                SyscallTotal = syscallResponse.TotalCount ?? syscallResponse.Items.Count,
                ContractCalls = contractResponse.Items,
                ContractCallTotal = contractResponse.TotalCount ?? contractResponse.Items.Count,
                StorageReads = storageReadsResponse.Items,
                StorageReadTotal = storageReadsResponse.TotalCount ?? storageReadsResponse.Items.Count,
                StorageWrites = storageWritesResponse.Items,
                StorageWriteTotal = storageWritesResponse.TotalCount ?? storageWritesResponse.Items.Count,
                Notifications = notificationsResponse.Items,
                NotificationTotal = notificationsResponse.TotalCount ?? notificationsResponse.Items.Count,
                Logs = logsResponse.Items,
                LogTotal = logsResponse.TotalCount ?? logsResponse.Items.Count
            };
        }

        private static List<KeyValuePair<string, string?>> BuildTraceQuery(uint blockIndex, string? transactionHash, TraceRequestOptions options)
        {
            var parameters = new List<KeyValuePair<string, string?>>
            {
                new("block_index", $"eq.{blockIndex}"),
                new("order", "trace_order.asc"),
                new("limit", options.Limit.ToString(CultureInfo.InvariantCulture)),
                new("offset", options.Offset.ToString(CultureInfo.InvariantCulture))
            };

            if (!string.IsNullOrEmpty(transactionHash))
                parameters.Insert(1, new KeyValuePair<string, string?>("tx_hash", $"eq.{transactionHash}"));

            return parameters;
        }

        private static List<KeyValuePair<string, string?>> BuildOrderedTraceQuery(
            uint blockIndex,
            string? transactionHash,
            string orderColumn,
            TraceRequestOptions options)
        {
            var parameters = new List<KeyValuePair<string, string?>>
            {
                new("block_index", $"eq.{blockIndex}"),
                new("order", $"{orderColumn}.asc"),
                new("limit", options.Limit.ToString(CultureInfo.InvariantCulture)),
                new("offset", options.Offset.ToString(CultureInfo.InvariantCulture))
            };

            if (!string.IsNullOrEmpty(transactionHash))
                parameters.Insert(1, new KeyValuePair<string, string?>("tx_hash", $"eq.{transactionHash}"));

            return parameters;
        }

        private static List<KeyValuePair<string, string?>> BuildTransactionResultsQuery(
            uint blockIndex,
            string? transactionHash,
            TraceRequestOptions options)
        {
            var parameters = new List<KeyValuePair<string, string?>>
            {
                new("block_index", $"eq.{blockIndex}"),
                new("order", "tx_hash.asc"),
                new("limit", options.Limit.ToString(CultureInfo.InvariantCulture)),
                new("offset", options.Offset.ToString(CultureInfo.InvariantCulture))
            };

            if (!string.IsNullOrEmpty(transactionHash))
                parameters.Insert(1, new KeyValuePair<string, string?>("tx_hash", $"eq.{transactionHash}"));

            return parameters;
        }
    }
}
