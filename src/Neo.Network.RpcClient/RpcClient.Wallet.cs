// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.Wallet.cs file belongs to the neo project and is free
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
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    public partial class RpcClient
    {
        #region Wallet

        /// <summary>
        /// Close the wallet opened by RPC.
        /// </summary>
        public async Task<bool> CloseWalletAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return result.AsBoolean();
        }

        /// <summary>
        /// Exports the private key of the specified address.
        /// </summary>
        public async Task<string> DumpPrivKeyAsync(string address)
        {
            var result = await RpcSendAsync(GetRpcName(), address).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Creates a new account in the wallet opened by RPC.
        /// </summary>
        public async Task<string> GetNewAddressAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the balance of the corresponding asset in the wallet, based on the specified asset Id.
        /// This method applies to assets that conform to NEP-17 standards.
        /// </summary>
        /// <returns>new address as string</returns>
        public async Task<BigDecimal> GetWalletBalanceAsync(string assetId)
        {
            var result = await RpcSendAsync(GetRpcName(), assetId).ConfigureAwait(false);
            BigInteger balance = BigInteger.Parse(result["balance"].AsString());
            byte decimals = await new Nep17API(this).DecimalsAsync(UInt160.Parse(assetId.AsScriptHash())).ConfigureAwait(false);
            return new BigDecimal(balance, decimals);
        }

        /// <summary>
        /// Gets the amount of unclaimed GAS in the wallet.
        /// </summary>
        public async Task<BigDecimal> GetWalletUnclaimedGasAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return BigDecimal.Parse(result.AsString(), NativeContract.GAS.Decimals);
        }

        /// <summary>
        /// Imports the private key to the wallet.
        /// </summary>
        public async Task<RpcAccount> ImportPrivKeyAsync(string wif)
        {
            var result = await RpcSendAsync(GetRpcName(), wif).ConfigureAwait(false);
            return RpcAccount.FromJson((JObject)result);
        }

        /// <summary>
        /// Lists all the accounts in the current wallet.
        /// </summary>
        public async Task<List<RpcAccount>> ListAddressAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return ((JArray)result).Select(p => RpcAccount.FromJson((JObject)p)).ToList();
        }

        /// <summary>
        /// Open wallet file in the provider's machine.
        /// By default, this method is disabled by RpcServer config.json.
        /// </summary>
        public async Task<bool> OpenWalletAsync(string path, string password)
        {
            var result = await RpcSendAsync(GetRpcName(), path, password).ConfigureAwait(false);
            return result.AsBoolean();
        }

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

        #endregion Wallet
    }
}
