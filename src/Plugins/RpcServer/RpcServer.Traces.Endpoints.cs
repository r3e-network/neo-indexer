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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
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

        [RpcMethod]
        protected internal virtual async Task<JToken> GetContractCalls(JArray _params)
        {
            if (_params.Count == 0)
                throw new RpcException(RpcError.InvalidParams.WithData("contract hash is required"));

            var rawHash = _params[0]?.AsString() ?? string.Empty;
            if (!UInt160.TryParse(rawHash, out var contractHash))
                throw new RpcException(RpcError.InvalidParams.WithData($"invalid contract hash: {rawHash}"));

            var options = ParseContractCallOptions(_params.Count > 1 ? _params[1] : null);
            var settings = EnsureSupabaseTraceSettings();

            var query = BuildContractCallQuery(contractHash.ToString(), options);
            var response = await SendSupabaseQueryAsync<ContractCallResult>(settings, "contract_calls", query).ConfigureAwait(false);

            JObject json = new();
            json["contractHash"] = contractHash.ToString();
            if (options.StartBlock.HasValue)
                json["startBlock"] = (int)options.StartBlock.Value;
            if (options.EndBlock.HasValue)
                json["endBlock"] = (int)options.EndBlock.Value;
            if (!string.IsNullOrEmpty(options.TransactionHash))
                json["transactionHash"] = options.TransactionHash;
            json["limit"] = options.Limit;
            json["offset"] = options.Offset;
            json["total"] = response.TotalCount ?? response.Items.Count;

            var calls = new JArray();
            foreach (var call in response.Items)
                calls.Add(call.ToJson());
            json["calls"] = calls;
            return json;
        }

        [RpcMethod]
        protected internal virtual async Task<JToken> GetSyscallStats(JArray _params)
        {
            SyscallStatsQueryOptions options;
            if (_params.Count >= 2 && _params[0] is not JObject)
            {
                options = new SyscallStatsQueryOptions
                {
                    StartBlock = ParseUIntParam(_params[0], "startBlock"),
                    EndBlock = ParseUIntParam(_params[1], "endBlock")
                };
                if (options.StartBlock.HasValue && options.EndBlock.HasValue && options.StartBlock > options.EndBlock)
                    throw new RpcException(RpcError.InvalidParams.WithData("startBlock cannot be greater than endBlock"));

                if (_params.Count > 2)
                {
                    var extra = ParseSyscallStatsOptions(_params[2]);
                    extra.StartBlock ??= options.StartBlock;
                    extra.EndBlock ??= options.EndBlock;
                    options = extra;
                }
            }
            else
            {
                options = ParseSyscallStatsOptions(_params.Count > 0 ? _params[0] : null);
            }
            var settings = EnsureSupabaseTraceSettings();

            if (!options.StartBlock.HasValue || !options.EndBlock.HasValue)
                throw new RpcException(RpcError.InvalidParams.WithData("startBlock and endBlock are required"));

            var payload = new Dictionary<string, object?>
            {
                ["start_block"] = (int)options.StartBlock.Value,
                ["end_block"] = (int)options.EndBlock.Value,
                ["p_contract_hash"] = options.ContractHash,
                ["p_transaction_hash"] = options.TransactionHash,
                ["p_syscall_name"] = options.SyscallName,
                ["limit_rows"] = options.Limit,
                ["offset_rows"] = options.Offset
            };

            var results = await SendSupabaseRpcAsync<SyscallStatsResult>(settings, "get_syscall_stats", payload).ConfigureAwait(false);

            JObject json = new();
            if (options.StartBlock.HasValue)
                json["startBlock"] = (int)options.StartBlock.Value;
            if (options.EndBlock.HasValue)
                json["endBlock"] = (int)options.EndBlock.Value;
            if (!string.IsNullOrEmpty(options.ContractHash))
                json["contractHash"] = options.ContractHash;
            if (!string.IsNullOrEmpty(options.TransactionHash))
                json["transactionHash"] = options.TransactionHash;
            if (!string.IsNullOrEmpty(options.SyscallName))
                json["syscallName"] = options.SyscallName;
            json["limit"] = options.Limit;
            json["offset"] = options.Offset;
            json["total"] = results.FirstOrDefault()?.TotalRows ?? results.Count;

            var stats = new JArray();
            foreach (var stat in results)
                stats.Add(stat.ToJson());
            json["stats"] = stats;
            return json;
        }

        [RpcMethod]
        protected internal virtual async Task<JToken> GetOpCodeStats(JArray _params)
        {
            OpCodeStatsQueryOptions options;
            if (_params.Count >= 2 && _params[0] is not JObject)
            {
                options = new OpCodeStatsQueryOptions
                {
                    StartBlock = ParseUIntParam(_params[0], "startBlock"),
                    EndBlock = ParseUIntParam(_params[1], "endBlock")
                };
                if (options.StartBlock.HasValue && options.EndBlock.HasValue && options.StartBlock > options.EndBlock)
                    throw new RpcException(RpcError.InvalidParams.WithData("startBlock cannot be greater than endBlock"));

                if (_params.Count > 2)
                {
                    var extra = ParseOpCodeStatsOptions(_params[2]);
                    extra.StartBlock ??= options.StartBlock;
                    extra.EndBlock ??= options.EndBlock;
                    options = extra;
                }
            }
            else
            {
                options = ParseOpCodeStatsOptions(_params.Count > 0 ? _params[0] : null);
            }
            var settings = EnsureSupabaseTraceSettings();

            if (!options.StartBlock.HasValue || !options.EndBlock.HasValue)
                throw new RpcException(RpcError.InvalidParams.WithData("startBlock and endBlock are required"));

            var payload = new Dictionary<string, object?>
            {
                ["start_block"] = (int)options.StartBlock.Value,
                ["end_block"] = (int)options.EndBlock.Value,
                ["p_contract_hash"] = options.ContractHash,
                ["p_transaction_hash"] = options.TransactionHash,
                ["p_opcode"] = options.OpCode,
                ["p_opcode_name"] = options.OpCodeName,
                ["limit_rows"] = options.Limit,
                ["offset_rows"] = options.Offset
            };

            var results = await SendSupabaseRpcAsync<OpCodeStatsResult>(settings, "get_opcode_stats", payload).ConfigureAwait(false);

            JObject json = new();
            if (options.StartBlock.HasValue)
                json["startBlock"] = (int)options.StartBlock.Value;
            if (options.EndBlock.HasValue)
                json["endBlock"] = (int)options.EndBlock.Value;
            if (!string.IsNullOrEmpty(options.ContractHash))
                json["contractHash"] = options.ContractHash;
            if (!string.IsNullOrEmpty(options.TransactionHash))
                json["transactionHash"] = options.TransactionHash;
            if (options.OpCode.HasValue)
                json["opcode"] = options.OpCode.Value;
            if (!string.IsNullOrEmpty(options.OpCodeName))
                json["opcodeName"] = options.OpCodeName;
            json["limit"] = options.Limit;
            json["offset"] = options.Offset;
            json["total"] = results.FirstOrDefault()?.TotalRows ?? results.Count;

            var stats = new JArray();
            foreach (var stat in results)
                stats.Add(stat.ToJson());
            json["stats"] = stats;
            return json;
        }

        [RpcMethod]
        protected internal virtual async Task<JToken> GetContractCallStats(JArray _params)
        {
            ContractCallStatsQueryOptions options;
            if (_params.Count >= 2 && _params[0] is not JObject)
            {
                options = new ContractCallStatsQueryOptions
                {
                    StartBlock = ParseUIntParam(_params[0], "startBlock"),
                    EndBlock = ParseUIntParam(_params[1], "endBlock")
                };
                if (options.StartBlock.HasValue && options.EndBlock.HasValue && options.StartBlock > options.EndBlock)
                    throw new RpcException(RpcError.InvalidParams.WithData("startBlock cannot be greater than endBlock"));

                if (_params.Count > 2)
                {
                    var extra = ParseContractCallStatsOptions(_params[2]);
                    extra.StartBlock ??= options.StartBlock;
                    extra.EndBlock ??= options.EndBlock;
                    options = extra;
                }
            }
            else
            {
                options = ParseContractCallStatsOptions(_params.Count > 0 ? _params[0] : null);
            }

            var settings = EnsureSupabaseTraceSettings();

            if (!options.StartBlock.HasValue || !options.EndBlock.HasValue)
                throw new RpcException(RpcError.InvalidParams.WithData("startBlock and endBlock are required"));

            var payload = new Dictionary<string, object?>
            {
                ["start_block"] = (int)options.StartBlock.Value,
                ["end_block"] = (int)options.EndBlock.Value,
                ["p_callee_hash"] = options.CalleeHash,
                ["p_caller_hash"] = options.CallerHash,
                ["p_method_name"] = options.MethodName,
                ["limit_rows"] = options.Limit,
                ["offset_rows"] = options.Offset
            };

            var results = await SendSupabaseRpcAsync<ContractCallStatsResult>(settings, "get_contract_call_stats", payload).ConfigureAwait(false);

            JObject json = new();
            json["startBlock"] = (int)options.StartBlock.Value;
            json["endBlock"] = (int)options.EndBlock.Value;
            if (!string.IsNullOrEmpty(options.CalleeHash))
                json["calleeHash"] = options.CalleeHash;
            if (!string.IsNullOrEmpty(options.CallerHash))
                json["callerHash"] = options.CallerHash;
            if (!string.IsNullOrEmpty(options.MethodName))
                json["methodName"] = options.MethodName;
            json["limit"] = options.Limit;
            json["offset"] = options.Offset;
            json["total"] = results.FirstOrDefault()?.TotalRows ?? results.Count;

            var stats = new JArray();
            foreach (var stat in results)
                stats.Add(stat.ToJson());
            json["stats"] = stats;
            return json;
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

        private static List<KeyValuePair<string, string?>> BuildContractCallQuery(string contractHash, ContractCallQueryOptions options)
        {
            var parameters = new List<KeyValuePair<string, string?>>();
            switch (options.Role)
            {
                case ContractCallRole.Caller:
                    parameters.Add(new("caller_hash", $"eq.{contractHash}"));
                    break;
                case ContractCallRole.Callee:
                    parameters.Add(new("callee_hash", $"eq.{contractHash}"));
                    break;
                default:
                    parameters.Add(new("or", $"(caller_hash.eq.{contractHash},callee_hash.eq.{contractHash})"));
                    break;
            }

            if (options.StartBlock.HasValue)
                parameters.Add(new("block_index", $"gte.{options.StartBlock.Value}"));
            if (options.EndBlock.HasValue)
                parameters.Add(new("block_index", $"lte.{options.EndBlock.Value}"));
            if (!string.IsNullOrEmpty(options.TransactionHash))
                parameters.Add(new("tx_hash", $"eq.{options.TransactionHash}"));

            parameters.Add(new("order", "trace_order.asc"));
            parameters.Add(new("limit", options.Limit.ToString(CultureInfo.InvariantCulture)));
            parameters.Add(new("offset", options.Offset.ToString(CultureInfo.InvariantCulture)));
            return parameters;
        }

    }
}
