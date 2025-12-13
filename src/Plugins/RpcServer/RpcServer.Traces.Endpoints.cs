// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Endpoints.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo;
using Neo.Extensions;
using Neo.Json;
using Neo.Persistence;
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        [RpcMethod]
        protected internal virtual async Task<JToken> GetBlockTrace(JArray _params)
        {
            if (_params.Count == 0)
                throw new RpcException(RpcError.InvalidParams.WithData("block hash or index is required"));

            var identifierToken = _params[0].NotNull_Or(RpcError.InvalidParams.WithData("block hash or index is required"));
            var identifier = ParseBlockIdentifier(identifierToken);
            var (blockIndex, blockHash) = ResolveBlock(identifier);
            var options = ParseTraceRequestOptions(_params.Count > 1 ? _params[1] : null, allowTransactionFilter: true);
            var settings = EnsureSupabaseTraceSettings();

            var traceResult = await QueryTraceResultAsync(settings, blockIndex, blockHash, options.TransactionHash, options).ConfigureAwait(false);
            return traceResult.ToJson();
        }

        [RpcMethod]
        protected internal virtual async Task<JToken> GetTransactionTrace(JArray _params)
        {
            if (_params.Count == 0)
                throw new RpcException(RpcError.InvalidParams.WithData("transaction hash is required"));

            var transactionToken = _params[0].NotNull_Or(RpcError.InvalidParams.WithData("transaction hash is required"));
            var transaction = ParseTransactionHash(transactionToken, "transaction hash");
            var snapshot = system.StoreView;
            var state = NativeContract.Ledger.GetTransactionState(snapshot, transaction.Hash).NotNull_Or(RpcError.UnknownTransaction);
            var blockHash = NativeContract.Ledger.GetBlockHash(snapshot, state.BlockIndex).NotNull_Or(RpcError.UnknownBlock).ToString();

            var options = ParseTraceRequestOptions(_params.Count > 1 ? _params[1] : null, allowTransactionFilter: false);
            var settings = EnsureSupabaseTraceSettings();

            var traceResult = await QueryTraceResultAsync(settings, state.BlockIndex, blockHash, transaction.HashString, options, transaction.HashString).ConfigureAwait(false);
            return traceResult.ToJson();
        }

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
            var opcodeQuery = BuildTraceQuery(blockIndex, txFilter, options);
            var syscallQuery = BuildTraceQuery(blockIndex, txFilter, options);
            var contractQuery = BuildTraceQuery(blockIndex, txFilter, options);

            var opcodeTask = SendSupabaseQueryAsync<OpCodeTraceResult>(settings, "opcode_traces", opcodeQuery);
            var syscallTask = SendSupabaseQueryAsync<SyscallTraceResult>(settings, "syscall_traces", syscallQuery);
            var contractTask = SendSupabaseQueryAsync<ContractCallResult>(settings, "contract_calls", contractQuery);

            await Task.WhenAll(opcodeTask, syscallTask, contractTask).ConfigureAwait(false);

            var opcodeResponse = await opcodeTask.ConfigureAwait(false);
            var syscallResponse = await syscallTask.ConfigureAwait(false);
            var contractResponse = await contractTask.ConfigureAwait(false);

            return new TraceResult
            {
                BlockIndex = blockIndex,
                BlockHash = blockHash,
                TransactionHash = explicitTransactionHash ?? txFilter,
                Limit = options.Limit,
                Offset = options.Offset,
                OpCodeTraces = opcodeResponse.Items,
                OpCodeTotal = opcodeResponse.TotalCount ?? opcodeResponse.Items.Count,
                SyscallTraces = syscallResponse.Items,
                SyscallTotal = syscallResponse.TotalCount ?? syscallResponse.Items.Count,
                ContractCalls = contractResponse.Items,
                ContractCallTotal = contractResponse.TotalCount ?? contractResponse.Items.Count
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

    }
}
