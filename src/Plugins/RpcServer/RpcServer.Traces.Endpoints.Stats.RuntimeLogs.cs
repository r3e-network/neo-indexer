// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Endpoints.Stats.RuntimeLogs.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Json;
using Neo.Plugins.RpcServer.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        [RpcMethod]
        protected internal virtual async Task<JToken> GetLogStats(JArray _params)
        {
            RuntimeLogStatsQueryOptions options;
            if (_params.Count >= 2 && _params[0] is not JObject)
            {
                options = new RuntimeLogStatsQueryOptions
                {
                    StartBlock = ParseUIntParam(_params[0], "startBlock"),
                    EndBlock = ParseUIntParam(_params[1], "endBlock")
                };
                if (options.StartBlock.HasValue && options.EndBlock.HasValue && options.StartBlock > options.EndBlock)
                    throw new RpcException(RpcError.InvalidParams.WithData("startBlock cannot be greater than endBlock"));

                if (_params.Count > 2)
                {
                    var extra = ParseRuntimeLogStatsOptions(_params[2]);
                    extra.StartBlock ??= options.StartBlock;
                    extra.EndBlock ??= options.EndBlock;
                    options = extra;
                }
            }
            else
            {
                options = ParseRuntimeLogStatsOptions(_params.Count > 0 ? _params[0] : null);
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
                ["limit_rows"] = options.Limit,
                ["offset_rows"] = options.Offset
            };

            var results = await SendSupabaseRpcAsync<RuntimeLogStatsResult>(settings, "get_runtime_log_stats", payload).ConfigureAwait(false);

            JObject json = new();
            json["startBlock"] = (int)options.StartBlock.Value;
            json["endBlock"] = (int)options.EndBlock.Value;
            if (!string.IsNullOrEmpty(options.ContractHash))
                json["contractHash"] = options.ContractHash;
            if (!string.IsNullOrEmpty(options.TransactionHash))
                json["transactionHash"] = options.TransactionHash;
            json["limit"] = options.Limit;
            json["offset"] = options.Offset;
            json["total"] = results.FirstOrDefault()?.TotalRows ?? results.Count;

            var stats = new JArray();
            foreach (var stat in results)
                stats.Add(stat.ToJson());
            json["stats"] = stats;
            return json;
        }
    }
}

