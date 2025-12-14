// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Wallet.Core.cs file belongs to the neo project and is free
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
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.IO;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private class DummyWallet : Wallet
        {
            public DummyWallet(ProtocolSettings settings) : base(null, settings) { }
            public override string Name => "";
            public override Version Version => new();

            public override bool ChangePassword(string oldPassword, string newPassword) => false;
            public override bool Contains(UInt160 scriptHash) => false;
            public override WalletAccount CreateAccount(byte[] privateKey) => null;
            public override WalletAccount CreateAccount(Contract contract, KeyPair key = null) => null;
            public override WalletAccount CreateAccount(UInt160 scriptHash) => null;
            public override void Delete() { }
            public override bool DeleteAccount(UInt160 scriptHash) => false;
            public override WalletAccount GetAccount(UInt160 scriptHash) => null;
            public override IEnumerable<WalletAccount> GetAccounts() => Array.Empty<WalletAccount>();
            public override bool VerifyPassword(string password) => false;
            public override void Save() { }
        }

        protected internal Wallet wallet;

        /// <summary>
        /// Checks if a wallet is open and throws an error if not.
        /// </summary>
        private void CheckWallet()
        {
            wallet.NotNull_Or(RpcError.NoOpenedWallet);
        }

        /// <summary>
        /// Closes the currently opened wallet.
        /// </summary>
        /// <param name="_params">An empty array.</param>
        /// <returns>Returns true if the wallet was successfully closed.</returns>
        [RpcMethod]
        protected internal virtual JToken CloseWallet(JArray _params)
        {
            wallet = null;
            return true;
        }

        /// <summary>
        /// Opens a wallet file.
        /// </summary>
        /// <param name="_params">An array containing the wallet path and password.</param>
        /// <returns>Returns true if the wallet was successfully opened.</returns>
        /// <exception cref="RpcException">Thrown when the wallet file is not found, the wallet is not supported, or the password is invalid.</exception>
        [RpcMethod]
        protected internal virtual JToken OpenWallet(JArray _params)
        {
            string path = _params[0].AsString();
            string password = _params[1].AsString();
            File.Exists(path).True_Or(RpcError.WalletNotFound);
            try
            {
                wallet = Wallet.Open(path, password, system.Settings).NotNull_Or(RpcError.WalletNotSupported);
            }
            catch (NullReferenceException)
            {
                throw new RpcException(RpcError.WalletNotSupported);
            }
            catch (InvalidOperationException)
            {
                throw new RpcException(RpcError.WalletNotSupported.WithData("Invalid password."));
            }

            return true;
        }

        /// <summary>
        /// Processes the result of an invocation with wallet for signing.
        /// </summary>
        /// <param name="result">The result object to process.</param>
        /// <param name="signers">Optional signers for the transaction.</param>
        private void ProcessInvokeWithWallet(JObject result, Signer[] signers = null)
        {
            if (wallet == null || signers == null || signers.Length == 0) return;

            UInt160 sender = signers[0].Account;
            Transaction tx;
            try
            {
                tx = wallet.MakeTransaction(system.StoreView, Convert.FromBase64String(result["script"].AsString()), sender, signers, maxGas: settings.MaxGasInvoke);
            }
            catch (Exception e)
            {
                result["exception"] = GetExceptionMessage(e);
                return;
            }
            ContractParametersContext context = new(system.StoreView, tx, settings.Network);
            wallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                result["tx"] = Convert.ToBase64String(tx.ToArray());
            }
            else
            {
                result["pendingsignature"] = context.ToJson();
            }
        }
    }
}

