// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.Node.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Extensions;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using System;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    public partial class RpcClient
    {
        #region Node

        /// <summary>
        /// Gets the current number of connections for the node.
        /// </summary>
        public async Task<int> GetConnectionCountAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return (int)result.AsNumber();
        }

        /// <summary>
        /// Gets the list of nodes that the node is currently connected/disconnected from.
        /// </summary>
        public async Task<RpcPeers> GetPeersAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return RpcPeers.FromJson((JObject)result);
        }

        /// <summary>
        /// Returns the version information about the queried node.
        /// </summary>
        public async Task<RpcVersion> GetVersionAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return RpcVersion.FromJson((JObject)result);
        }

        /// <summary>
        /// Broadcasts a serialized transaction over the NEO network.
        /// </summary>
        public async Task<UInt256> SendRawTransactionAsync(byte[] rawTransaction)
        {
            var result = await RpcSendAsync(GetRpcName(), Convert.ToBase64String(rawTransaction)).ConfigureAwait(false);
            return UInt256.Parse(result["hash"].AsString());
        }

        /// <summary>
        /// Broadcasts a transaction over the NEO network.
        /// </summary>
        public Task<UInt256> SendRawTransactionAsync(Transaction transaction)
        {
            return SendRawTransactionAsync(transaction.ToArray());
        }

        /// <summary>
        /// Broadcasts a serialized block over the NEO network.
        /// </summary>
        public async Task<UInt256> SubmitBlockAsync(byte[] block)
        {
            var result = await RpcSendAsync(GetRpcName(), Convert.ToBase64String(block)).ConfigureAwait(false);
            return UInt256.Parse(result["hash"].AsString());
        }

        #endregion Node
    }
}
