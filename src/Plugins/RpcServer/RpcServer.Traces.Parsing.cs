// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Parsing.cs file belongs to the neo project and is free
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
using Neo.Plugins.RpcServer.Model;
using System;
using System.Globalization;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
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

        private static ContractCallStatsQueryOptions ParseContractCallStatsOptions(JToken? token)
        {
            var options = new ContractCallStatsQueryOptions();
            if (token is not JObject obj)
                return options;

            options.Limit = NormalizeLimit(TryParseInt(obj, "limit"));
            options.Offset = NormalizeOffset(TryParseInt(obj, "offset"));
            options.StartBlock = TryParseUInt(obj, "startBlock");
            options.EndBlock = TryParseUInt(obj, "endBlock");
            if (options.StartBlock.HasValue && options.EndBlock.HasValue && options.StartBlock > options.EndBlock)
                throw new RpcException(RpcError.InvalidParams.WithData("startBlock cannot be greater than endBlock"));

            if (obj.ContainsProperty("calleeHash"))
            {
                var raw = obj["calleeHash"]?.AsString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (!UInt160.TryParse(raw, out var calleeHash))
                        throw new RpcException(RpcError.InvalidParams.WithData($"invalid callee hash: {raw}"));
                    options.CalleeHash = calleeHash.ToString();
                }
            }

            if (obj.ContainsProperty("callerHash"))
            {
                var raw = obj["callerHash"]?.AsString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (!UInt160.TryParse(raw, out var callerHash))
                        throw new RpcException(RpcError.InvalidParams.WithData($"invalid caller hash: {raw}"));
                    options.CallerHash = callerHash.ToString();
                }
            }

            if (obj.ContainsProperty("methodName"))
            {
                var name = obj["methodName"]?.AsString();
                if (!string.IsNullOrWhiteSpace(name))
                    options.MethodName = name;
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
    }
}
