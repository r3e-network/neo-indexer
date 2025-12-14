// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.Blockchain.Mempool.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Network.RPC.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    public partial class RpcClient
    {
        #region Blockchain - Mempool

        /// <summary>
        /// Obtains the list of unconfirmed transactions in memory.
        /// </summary>
        public async Task<string[]> GetRawMempoolAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return ((JArray)result).Select(p => p.AsString()).ToArray();
        }

        /// <summary>
        /// Obtains the list of unconfirmed transactions in memory.
        /// shouldGetUnverified = true
        /// </summary>
        public async Task<RpcRawMemPool> GetRawMempoolBothAsync()
        {
            var result = await RpcSendAsync(GetRpcName(), true).ConfigureAwait(false);
            return RpcRawMemPool.FromJson((JObject)result);
        }

        #endregion Blockchain - Mempool
    }
}
