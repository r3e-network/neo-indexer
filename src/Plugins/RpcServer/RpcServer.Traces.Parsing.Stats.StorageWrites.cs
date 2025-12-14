// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Parsing.Stats.StorageWrites.cs file belongs to the neo project and is free
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

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private static StorageWriteStatsQueryOptions ParseStorageWriteStatsOptions(JToken? token)
        {
            var options = new StorageWriteStatsQueryOptions();
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
            return options;
        }
    }
}

