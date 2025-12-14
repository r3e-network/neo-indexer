// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Parsing.Stats.ContractCalls.cs file belongs to the neo project and is free
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
using System;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
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
    }
}

