// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Endpoints.TransactionResults.cs file belongs to the neo project and is free
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
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract.Native;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        [RpcMethod]
        protected internal virtual async Task<JToken> GetTransactionResult(JArray _params)
        {
            if (_params.Count == 0)
                throw new RpcException(RpcError.InvalidParams.WithData("transaction hash is required"));

            var transactionToken = _params[0].NotNull_Or(RpcError.InvalidParams.WithData("transaction hash is required"));
            var transaction = ParseTransactionHash(transactionToken, "transaction hash");

            var snapshot = system.StoreView;
            var state = NativeContract.Ledger.GetTransactionState(snapshot, transaction.Hash).NotNull_Or(RpcError.UnknownTransaction);
            var blockHash = NativeContract.Ledger.GetBlockHash(snapshot, state.BlockIndex).NotNull_Or(RpcError.UnknownBlock).ToString();

            var settings = EnsureSupabaseTraceSettings();

            var query = new List<KeyValuePair<string, string?>>
            {
                new("block_index", $"eq.{state.BlockIndex}"),
                new("tx_hash", $"eq.{transaction.HashString}"),
                new("limit", "1")
            };

            var response = await SendSupabaseQueryAsync<TransactionResultResult>(settings, "transaction_results", query).ConfigureAwait(false);

            if (response.Items.Count == 0)
            {
                JObject missing = new();
                missing["indexed"] = false;
                missing["blockIndex"] = (int)state.BlockIndex;
                missing["blockHash"] = blockHash;
                missing["transactionHash"] = transaction.HashString;
                return missing;
            }

            var result = response.Items[0].ToJson();
            result["indexed"] = true;
            result["blockHash"] = blockHash;
            return result;
        }
    }
}

