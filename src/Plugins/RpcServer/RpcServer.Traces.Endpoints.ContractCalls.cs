// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Endpoints.ContractCalls.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo;
using Neo.Json;
using Neo.Plugins.RpcServer.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
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

