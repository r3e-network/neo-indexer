// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Wallet.Transactions.Fees.cs file belongs to the neo project and is free
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
using System;
using Helper = Neo.Wallets.Helper;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Calculates the network fee for a given transaction.
        /// </summary>
        /// <param name="_params">An array containing the Base64-encoded serialized transaction.</param>
        /// <returns>A JSON object containing the calculated network fee.</returns>
        /// <exception cref="RpcException">Thrown when the input parameters are invalid or the transaction is malformed.</exception>
        [RpcMethod]
        protected internal virtual JToken CalculateNetworkFee(JArray _params)
        {
            if (_params.Count == 0)
            {
                throw new RpcException(RpcError.InvalidParams.WithData("Params array is empty, need a raw transaction."));
            }
            var tx = Result.Ok_Or(() => Convert.FromBase64String(_params[0].AsString()), RpcError.InvalidParams.WithData($"Invalid tx: {_params[0]}")); ;

            JObject account = new();
            var networkfee = Helper.CalculateNetworkFee(tx.AsSerializable<Transaction>(), system.StoreView, system.Settings, wallet);
            account["networkfee"] = networkfee.ToString();
            return account;
        }
    }
}
