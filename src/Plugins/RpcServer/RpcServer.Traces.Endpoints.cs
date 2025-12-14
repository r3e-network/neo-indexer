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
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract.Native;
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
    }
}
