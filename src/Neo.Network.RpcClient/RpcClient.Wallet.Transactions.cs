// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.Wallet.Transactions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Json;
using Neo.Network.RPC.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    public partial class RpcClient
    {
        #region Wallet - Transactions

        /// <summary>
        /// Transfer from the specified address to the destination address.
        /// </summary>
        /// <returns>This function returns Signed Transaction JSON if successful, ContractParametersContext JSON if signing failed.</returns>
        public async Task<JObject> SendFromAsync(string assetId, string fromAddress, string toAddress, string amount)
        {
            return (JObject)await RpcSendAsync(GetRpcName(), assetId.AsScriptHash(), fromAddress.AsScriptHash(),
                                      toAddress.AsScriptHash(), amount).ConfigureAwait(false);
        }

        /// <summary>
        /// Bulk transfer order, and you can specify a sender address.
        /// </summary>
        /// <returns>This function returns Signed Transaction JSON if successful, ContractParametersContext JSON if signing failed.</returns>
        public async Task<JObject> SendManyAsync(string fromAddress, IEnumerable<RpcTransferOut> outputs)
        {
            var parameters = new List<JToken>();
            if (!string.IsNullOrEmpty(fromAddress))
            {
                parameters.Add(fromAddress.AsScriptHash());
            }
            parameters.Add(outputs.Select(p => p.ToJson(protocolSettings)).ToArray());

            return (JObject)await RpcSendAsync(GetRpcName(), paraArgs: parameters.ToArray()).ConfigureAwait(false);
        }

        /// <summary>
        /// Transfer asset from the wallet to the destination address.
        /// </summary>
        /// <returns>This function returns Signed Transaction JSON if successful, ContractParametersContext JSON if signing failed.</returns>
        public async Task<JObject> SendToAddressAsync(string assetId, string address, string amount)
        {
            return (JObject)await RpcSendAsync(GetRpcName(), assetId.AsScriptHash(), address.AsScriptHash(), amount)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Cancel Tx.
        /// </summary>
        /// <returns>This function returns Signed Transaction JSON if successful, ContractParametersContext JSON if signing failed.</returns>
        public async Task<JObject> CancelTransactionAsync(UInt256 txId, string[] signers, string extraFee)
        {
            JToken[] parameters = signers.Select(s => (JString)s.AsScriptHash()).ToArray();
            return (JObject)await RpcSendAsync(GetRpcName(), txId.ToString(), new JArray(parameters), extraFee).ConfigureAwait(false);
        }

        #endregion Wallet - Transactions
    }
}

