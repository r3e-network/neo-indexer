// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.cs file belongs to the neo project and is free
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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private const int DefaultTraceLimit = 1000;
        private const int MaxTraceLimit = 5000;
        private static readonly HttpClient TraceHttpClient = new();
        private static readonly JsonSerializerOptions TraceSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

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

            var query = BuildSyscallStatsQuery(options);
            var response = await SendSupabaseQueryAsync<SyscallStatsResult>(settings, "syscall_traces", query).ConfigureAwait(false);

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
            json["total"] = response.TotalCount ?? response.Items.Count;

            var stats = new JArray();
            foreach (var stat in response.Items)
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

            var query = BuildOpCodeStatsQuery(options);
            var response = await SendSupabaseQueryAsync<OpCodeStatsResult>(settings, "opcode_traces", query).ConfigureAwait(false);

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
            json["total"] = response.TotalCount ?? response.Items.Count;

            var stats = new JArray();
            foreach (var stat in response.Items)
                stats.Add(stat.ToJson());
            json["stats"] = stats;
            return json;
        }

        private static BlockHashOrIndex ParseBlockIdentifier(JToken token)
        {
            if (token is null)
                throw new RpcException(RpcError.InvalidParams.WithData("block hash or index is required"));

            if (token is JNumber)
            {
                var number = token.AsNumber();
                if (double.IsNaN(number) || number < 0 || number > uint.MaxValue)
                    throw new RpcException(RpcError.InvalidParams.WithData($"invalid block index: {token}"));
                if (Math.Abs(number % 1) > double.Epsilon)
                    throw new RpcException(RpcError.InvalidParams.WithData("block index must be an integer"));
                return new BlockHashOrIndex((uint)number);
            }

            var raw = token.AsString();
            if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                return new BlockHashOrIndex(index);
            if (UInt256.TryParse(raw, out var hash))
                return new BlockHashOrIndex(hash);
            throw new RpcException(RpcError.InvalidParams.WithData($"invalid block hash or index: {raw}"));
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

        private static TraceRequestOptions ParseTraceRequestOptions(JToken? token, bool allowTransactionFilter)
        {
            var options = new TraceRequestOptions();
            if (token is not JObject obj)
                return options;

            options.Limit = NormalizeLimit(TryParseInt(obj, "limit"));
            options.Offset = NormalizeOffset(TryParseInt(obj, "offset"));
            if (allowTransactionFilter)
                options.TransactionHash = ParseTransactionHashFilter(obj);

            return options;
        }

        private static string? ParseTransactionHashFilter(JObject obj)
        {
            if (!obj.ContainsProperty("transactionHash"))
                return null;
            var raw = obj["transactionHash"]?.AsString();
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            if (!UInt256.TryParse(raw, out var hash))
                throw new RpcException(RpcError.InvalidParams.WithData($"invalid transaction hash: {raw}"));
            return hash.ToString();
        }

        private static ContractCallQueryOptions ParseContractCallOptions(JToken? token)
        {
            var options = new ContractCallQueryOptions();
            if (token is not JObject obj)
                return options;

            options.Limit = NormalizeLimit(TryParseInt(obj, "limit"));
            options.Offset = NormalizeOffset(TryParseInt(obj, "offset"));
            options.StartBlock = TryParseUInt(obj, "startBlock");
            options.EndBlock = TryParseUInt(obj, "endBlock");
            if (options.StartBlock.HasValue && options.EndBlock.HasValue && options.StartBlock > options.EndBlock)
                throw new RpcException(RpcError.InvalidParams.WithData("startBlock cannot be greater than endBlock"));

            if (obj.ContainsProperty("role"))
            {
                var role = obj["role"]?.AsString();
                options.Role = role?.ToLowerInvariant() switch
                {
                    "caller" => ContractCallRole.Caller,
                    "callee" => ContractCallRole.Callee,
                    _ => ContractCallRole.Any
                };
            }

            options.TransactionHash = ParseTransactionHashFilter(obj);
            return options;
        }

        private static SyscallStatsQueryOptions ParseSyscallStatsOptions(JToken? token)
        {
            var options = new SyscallStatsQueryOptions();
            if (token is not JObject obj)
                return options;

            options.Limit = NormalizeLimit(TryParseInt(obj, "limit"));
            options.Offset = NormalizeOffset(TryParseInt(obj, "offset"));
            options.StartBlock = TryParseUInt(obj, "startBlock");
            options.EndBlock = TryParseUInt(obj, "endBlock");
            if (options.StartBlock.HasValue && options.EndBlock.HasValue && options.StartBlock > options.EndBlock)
                throw new RpcException(RpcError.InvalidParams.WithData("startBlock cannot be greater than endBlock"));

            if (obj.ContainsProperty("contractHash"))
            {
                var raw = obj["contractHash"]?.AsString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (!UInt160.TryParse(raw, out var contractHash))
                        throw new RpcException(RpcError.InvalidParams.WithData($"invalid contract hash: {raw}"));
                    options.ContractHash = contractHash.ToString();
                }
            }

            options.TransactionHash = ParseTransactionHashFilter(obj);
            if (obj.ContainsProperty("syscallName"))
            {
                var name = obj["syscallName"]?.AsString();
                if (!string.IsNullOrWhiteSpace(name))
                    options.SyscallName = name;
            }

            return options;
        }

        private static OpCodeStatsQueryOptions ParseOpCodeStatsOptions(JToken? token)
        {
            var options = new OpCodeStatsQueryOptions();
            if (token is not JObject obj)
                return options;

            options.Limit = NormalizeLimit(TryParseInt(obj, "limit"));
            options.Offset = NormalizeOffset(TryParseInt(obj, "offset"));
            options.StartBlock = TryParseUInt(obj, "startBlock");
            options.EndBlock = TryParseUInt(obj, "endBlock");
            if (options.StartBlock.HasValue && options.EndBlock.HasValue && options.StartBlock > options.EndBlock)
                throw new RpcException(RpcError.InvalidParams.WithData("startBlock cannot be greater than endBlock"));

            if (obj.ContainsProperty("contractHash"))
            {
                var raw = obj["contractHash"]?.AsString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (!UInt160.TryParse(raw, out var contractHash))
                        throw new RpcException(RpcError.InvalidParams.WithData($"invalid contract hash: {raw}"));
                    options.ContractHash = contractHash.ToString();
                }
            }

            options.TransactionHash = ParseTransactionHashFilter(obj);

            if (obj.ContainsProperty("opcodeName"))
            {
                var name = obj["opcodeName"]?.AsString();
                if (!string.IsNullOrWhiteSpace(name))
                    options.OpCodeName = name;
            }

            if (obj.ContainsProperty("opcode"))
            {
                var raw = obj["opcode"]?.AsString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(raw.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
                    {
                        options.OpCode = hex;
                    }
                    else if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    {
                        options.OpCode = parsed;
                    }
                    else
                    {
                        throw new RpcException(RpcError.InvalidParams.WithData($"invalid opcode: {raw}"));
                    }
                }
            }

            return options;
        }

        private static int NormalizeLimit(int? value)
        {
            if (!value.HasValue)
                return DefaultTraceLimit;
            if (value.Value <= 0)
                throw new RpcException(RpcError.InvalidParams.WithData("limit must be positive"));
            return Math.Min(value.Value, MaxTraceLimit);
        }

        private static int NormalizeOffset(int? value)
        {
            if (!value.HasValue)
                return 0;
            return value.Value < 0 ? 0 : value.Value;
        }

        private static int? TryParseInt(JObject obj, string propertyName)
        {
            if (!obj.ContainsProperty(propertyName))
                return null;
            var token = obj[propertyName];
            if (token is null)
                return null;
            var raw = token.AsString();
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            throw new RpcException(RpcError.InvalidParams.WithData($"invalid value for {propertyName}: {raw}"));
        }

        private static uint? TryParseUInt(JObject obj, string propertyName)
        {
            if (!obj.ContainsProperty(propertyName))
                return null;
            var token = obj[propertyName];
            if (token is null)
                return null;
            var raw = token.AsString();
            if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            throw new RpcException(RpcError.InvalidParams.WithData($"invalid value for {propertyName}: {raw}"));
        }

        private static uint? ParseUIntParam(JToken? token, string parameterName)
        {
            if (token is null)
                return null;

            var raw = token.AsString();
            if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            throw new RpcException(RpcError.InvalidParams.WithData($"invalid value for {parameterName}: {raw}"));
        }

        private static (UInt256 Hash, string HashString) ParseTransactionHash(JToken token, string parameterName)
        {
            var raw = token?.AsString() ?? string.Empty;
            if (!UInt256.TryParse(raw, out var hash))
                throw new RpcException(RpcError.InvalidParams.WithData($"invalid {parameterName}: {raw}"));
            return (hash, hash.ToString());
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

        private static List<KeyValuePair<string, string?>> BuildSyscallStatsQuery(SyscallStatsQueryOptions options)
        {
            const string select =
                "syscall_name,call_count:count(*),total_gas_cost:sum(gas_cost),avg_gas_cost:avg(gas_cost),min_gas_cost:min(gas_cost),max_gas_cost:max(gas_cost),first_block:min(block_index),last_block:max(block_index)";

            var parameters = new List<KeyValuePair<string, string?>>
            {
                new("select", select),
                new("groupby", "syscall_name"),
                new("order", "call_count.desc"),
                new("limit", options.Limit.ToString(CultureInfo.InvariantCulture)),
                new("offset", options.Offset.ToString(CultureInfo.InvariantCulture))
            };

            if (options.StartBlock.HasValue)
                parameters.Add(new("block_index", $"gte.{options.StartBlock.Value}"));
            if (options.EndBlock.HasValue)
                parameters.Add(new("block_index", $"lte.{options.EndBlock.Value}"));
            if (!string.IsNullOrEmpty(options.ContractHash))
                parameters.Add(new("contract_hash", $"eq.{options.ContractHash}"));
            if (!string.IsNullOrEmpty(options.TransactionHash))
                parameters.Add(new("tx_hash", $"eq.{options.TransactionHash}"));
            if (!string.IsNullOrEmpty(options.SyscallName))
                parameters.Add(new("syscall_name", $"eq.{options.SyscallName}"));

            return parameters;
        }

        private static List<KeyValuePair<string, string?>> BuildOpCodeStatsQuery(OpCodeStatsQueryOptions options)
        {
            const string select =
                "opcode,opcode_name,call_count:count(*),total_gas_consumed:sum(gas_consumed),avg_gas_consumed:avg(gas_consumed),min_gas_consumed:min(gas_consumed),max_gas_consumed:max(gas_consumed),first_block:min(block_index),last_block:max(block_index)";

            var parameters = new List<KeyValuePair<string, string?>>
            {
                new("select", select),
                new("groupby", "opcode,opcode_name"),
                new("order", "call_count.desc"),
                new("limit", options.Limit.ToString(CultureInfo.InvariantCulture)),
                new("offset", options.Offset.ToString(CultureInfo.InvariantCulture))
            };

            if (options.StartBlock.HasValue)
                parameters.Add(new("block_index", $"gte.{options.StartBlock.Value}"));
            if (options.EndBlock.HasValue)
                parameters.Add(new("block_index", $"lte.{options.EndBlock.Value}"));
            if (!string.IsNullOrEmpty(options.ContractHash))
                parameters.Add(new("contract_hash", $"eq.{options.ContractHash}"));
            if (!string.IsNullOrEmpty(options.TransactionHash))
                parameters.Add(new("tx_hash", $"eq.{options.TransactionHash}"));
            if (options.OpCode.HasValue)
                parameters.Add(new("opcode", $"eq.{options.OpCode.Value}"));
            if (!string.IsNullOrEmpty(options.OpCodeName))
                parameters.Add(new("opcode_name", $"eq.{options.OpCodeName}"));

            return parameters;
        }

        private static StateRecorderSettings EnsureSupabaseTraceSettings()
        {
            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled)
                throw new RpcException(RpcError.InternalServerError.WithData("state recorder not enabled"));
            if (string.IsNullOrWhiteSpace(settings.SupabaseUrl) || string.IsNullOrWhiteSpace(settings.SupabaseApiKey))
                throw new RpcException(RpcError.InternalServerError.WithData("supabase connection not configured"));
            return settings;
        }

        private async Task<SupabaseResponse<T>> SendSupabaseQueryAsync<T>(StateRecorderSettings settings, string resource, IEnumerable<KeyValuePair<string, string?>> queryParams)
        {
            var uri = BuildSupabaseUri(settings.SupabaseUrl, resource, queryParams);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            ApplySupabaseHeaders(request, settings.SupabaseApiKey);

            using var response = await TraceHttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new RpcException(RpcError.InternalServerError.WithData($"Supabase request failed ({(int)response.StatusCode}): {payload}"));

            List<T>? items;
            try
            {
                items = JsonSerializer.Deserialize<List<T>>(payload, TraceSerializerOptions);
            }
            catch (JsonException ex)
            {
                throw new RpcException(RpcError.InternalServerError.WithData($"Failed to parse Supabase response: {ex.Message}"));
            }

            var total = TryParseTotalCount(response) ?? items?.Count ?? 0;
            return new SupabaseResponse<T>(items ?? new List<T>(), total);
        }

        private static string BuildSupabaseUri(string? baseUrl, string resource, IEnumerable<KeyValuePair<string, string?>> queryParams)
        {
            var trimmedBase = (baseUrl ?? string.Empty).TrimEnd('/');
            if (string.IsNullOrEmpty(trimmedBase))
                throw new RpcException(RpcError.InternalServerError.WithData("supabase url not configured"));

            StringBuilder builder = new();
            builder.Append(trimmedBase);
            builder.Append("/rest/v1/");
            builder.Append(resource);

            bool first = true;
            foreach (var (key, value) in queryParams)
            {
                if (string.IsNullOrEmpty(value))
                    continue;
                builder.Append(first ? '?' : '&');
                first = false;
                builder.Append(Uri.EscapeDataString(key));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(value));
            }

            return builder.ToString();
        }

        private static void ApplySupabaseHeaders(HttpRequestMessage request, string apiKey)
        {
            request.Headers.TryAddWithoutValidation("apikey", apiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Prefer", "count=exact");
        }

        private static int? TryParseTotalCount(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Content-Range", out var values))
                return null;
            var raw = values.FirstOrDefault();
            if (string.IsNullOrEmpty(raw))
                return null;
            var slashIndex = raw.LastIndexOf('/');
            if (slashIndex < 0 || slashIndex == raw.Length - 1)
                return null;
            var totalPart = raw[(slashIndex + 1)..];
            if (totalPart == "*")
                return null;
            return int.TryParse(totalPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var total) ? total : null;
        }

        private sealed class TraceRequestOptions
        {
            public int Limit { get; set; } = DefaultTraceLimit;
            public int Offset { get; set; }
            public string? TransactionHash { get; set; }
        }

        private enum ContractCallRole
        {
            Any,
            Caller,
            Callee
        }

        private sealed class ContractCallQueryOptions
        {
            public int Limit { get; set; } = DefaultTraceLimit;
            public int Offset { get; set; }
            public uint? StartBlock { get; set; }
            public uint? EndBlock { get; set; }
            public string? TransactionHash { get; set; }
            public ContractCallRole Role { get; set; } = ContractCallRole.Any;
        }

        private sealed class SyscallStatsQueryOptions
        {
            public int Limit { get; set; } = DefaultTraceLimit;
            public int Offset { get; set; }
            public uint? StartBlock { get; set; }
            public uint? EndBlock { get; set; }
            public string? ContractHash { get; set; }
            public string? TransactionHash { get; set; }
            public string? SyscallName { get; set; }
        }

        private sealed class OpCodeStatsQueryOptions
        {
            public int Limit { get; set; } = DefaultTraceLimit;
            public int Offset { get; set; }
            public uint? StartBlock { get; set; }
            public uint? EndBlock { get; set; }
            public string? ContractHash { get; set; }
            public string? TransactionHash { get; set; }
            public int? OpCode { get; set; }
            public string? OpCodeName { get; set; }
        }

        private sealed class SupabaseResponse<T>
        {
            public SupabaseResponse(IReadOnlyList<T> items, int totalCount)
            {
                Items = items;
                TotalCount = totalCount;
            }

            public IReadOnlyList<T> Items { get; }
            public int? TotalCount { get; }
        }
    }
}
