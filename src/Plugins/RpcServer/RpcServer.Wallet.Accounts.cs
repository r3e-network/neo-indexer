// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Wallet.Accounts.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System.Linq;
using System.Numerics;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Exports the private key of a specified address.
        /// </summary>
        /// <param name="_params">An array containing the address as a string.</param>
        /// <returns>The exported private key as a string.</returns>
        /// <exception cref="RpcException">Thrown when no wallet is open or the address is invalid.</exception>
        [RpcMethod]
        protected internal virtual JToken DumpPrivKey(JArray _params)
        {
            CheckWallet();
            UInt160 scriptHash = AddressToScriptHash(_params[0].AsString(), system.Settings.AddressVersion);
            WalletAccount account = wallet.GetAccount(scriptHash);
            return account.GetKey().Export();
        }

        /// <summary>
        /// Creates a new address in the wallet.
        /// </summary>
        /// <param name="_params">An empty array.</param>
        /// <returns>The newly created address as a string.</returns>
        /// <exception cref="RpcException">Thrown when no wallet is open.</exception>
        [RpcMethod]
        protected internal virtual JToken GetNewAddress(JArray _params)
        {
            CheckWallet();
            WalletAccount account = wallet.CreateAccount();
            if (wallet is NEP6Wallet nep6)
                nep6.Save();
            return account.Address;
        }

        /// <summary>
        /// Gets the balance of a specified asset in the wallet.
        /// </summary>
        /// <param name="_params">An array containing the asset ID as a string.</param>
        /// <returns>A JSON object containing the balance of the specified asset.</returns>
        /// <exception cref="RpcException">Thrown when no wallet is open or the asset ID is invalid.</exception>
        [RpcMethod]
        protected internal virtual JToken GetWalletBalance(JArray _params)
        {
            CheckWallet();
            UInt160 asset_id = Result.Ok_Or(() => UInt160.Parse(_params[0].AsString()), RpcError.InvalidParams.WithData($"Invalid asset id: {_params[0]}"));
            JObject json = new();
            json["balance"] = wallet.GetAvailable(system.StoreView, asset_id).Value.ToString();
            return json;
        }

        /// <summary>
        /// Gets the amount of unclaimed GAS in the wallet.
        /// </summary>
        /// <param name="_params">An empty array.</param>
        /// <returns>The amount of unclaimed GAS as a string.</returns>
        /// <exception cref="RpcException">Thrown when no wallet is open.</exception>
        [RpcMethod]
        protected internal virtual JToken GetWalletUnclaimedGas(JArray _params)
        {
            CheckWallet();
            // Datoshi is the smallest unit of GAS, 1 GAS = 10^8 Datoshi
            BigInteger datoshi = BigInteger.Zero;
            using (var snapshot = system.GetSnapshotCache())
            {
                uint height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
                foreach (UInt160 account in wallet.GetAccounts().Select(p => p.ScriptHash))
                    datoshi += NativeContract.NEO.UnclaimedGas(snapshot, account, height);
            }
            return datoshi.ToString();
        }

        /// <summary>
        /// Imports a private key into the wallet.
        /// </summary>
        /// <param name="_params">An array containing the private key as a string.</param>
        /// <returns>A JSON object containing information about the imported account.</returns>
        /// <exception cref="RpcException">Thrown when no wallet is open or the private key is invalid.</exception>
        [RpcMethod]
        protected internal virtual JToken ImportPrivKey(JArray _params)
        {
            CheckWallet();
            string privkey = _params[0].AsString();
            WalletAccount account = wallet.Import(privkey);
            if (wallet is NEP6Wallet nep6wallet)
                nep6wallet.Save();
            return new JObject
            {
                ["address"] = account.Address,
                ["haskey"] = account.HasKey,
                ["label"] = account.Label,
                ["watchonly"] = account.WatchOnly
            };
        }

        /// <summary>
        /// Lists all addresses in the wallet.
        /// </summary>
        /// <param name="_params">An empty array.</param>
        /// <returns>An array of JSON objects, each containing information about an address in the wallet.</returns>
        /// <exception cref="RpcException">Thrown when no wallet is open.</exception>
        [RpcMethod]
        protected internal virtual JToken ListAddress(JArray _params)
        {
            CheckWallet();
            return wallet.GetAccounts().Select(p =>
            {
                JObject account = new();
                account["address"] = p.Address;
                account["haskey"] = p.HasKey;
                account["label"] = p.Label;
                account["watchonly"] = p.WatchOnly;
                return account;
            }).ToArray();
        }
    }
}

