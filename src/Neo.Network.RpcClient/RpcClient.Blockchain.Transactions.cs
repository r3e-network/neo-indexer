// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.Blockchain.Transactions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using System;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    public partial class RpcClient
    {
        #region Blockchain - Transactions

        /// <summary>
        /// Returns the corresponding transaction information, based on the specified hash value.
        /// </summary>
        public async Task<string> GetRawTransactionHexAsync(string txHash)
        {
            var result = await RpcSendAsync(GetRpcName(), txHash).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the corresponding transaction information, based on the specified hash value.
        /// verbose = true
        /// </summary>
        public async Task<RpcTransaction> GetRawTransactionAsync(string txHash)
        {
            var result = await RpcSendAsync(GetRpcName(), txHash, true).ConfigureAwait(false);
            return RpcTransaction.FromJson((JObject)result, protocolSettings);
        }

        /// <summary>
        /// Calculate network fee
        /// </summary>
        /// <param name="tx">Transaction</param>
        /// <returns>NetworkFee</returns>
        public async Task<long> CalculateNetworkFeeAsync(Transaction tx)
        {
            var json = await RpcSendAsync(GetRpcName(), Convert.ToBase64String(tx.ToArray()))
                .ConfigureAwait(false);
            return (long)json["networkfee"].AsNumber();
        }

        /// <summary>
        /// Returns the block index in which the transaction is found.
        /// </summary>
        public async Task<uint> GetTransactionHeightAsync(string txHash)
        {
            var result = await RpcSendAsync(GetRpcName(), txHash).ConfigureAwait(false);
            return uint.Parse(result.AsString());
        }

        #endregion Blockchain - Transactions
    }
}
