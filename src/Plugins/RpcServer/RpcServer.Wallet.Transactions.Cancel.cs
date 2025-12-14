// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Wallet.Transactions.Cancel.cs file belongs to the neo project and is free
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
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Linq;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Cancels an unconfirmed transaction.
        /// </summary>
        /// <param name="_params">An array containing the transaction ID to cancel, signers, and optional extra fee.</param>
        /// <returns>The details of the cancellation transaction.</returns>
        /// <exception cref="RpcException">Thrown when no wallet is open, the transaction is already confirmed, or there are insufficient funds for the cancellation fee.</exception>
        [RpcMethod]
        protected internal virtual JToken CancelTransaction(JArray _params)
        {
            CheckWallet();
            var txid = Result.Ok_Or(() => UInt256.Parse(_params[0].AsString()), RpcError.InvalidParams.WithData($"Invalid txid: {_params[0]}"));
            NativeContract.Ledger.GetTransactionState(system.StoreView, txid).Null_Or(RpcErrorFactory.AlreadyExists("This tx is already confirmed, can't be cancelled."));

            var conflict = new TransactionAttribute[] { new Conflicts() { Hash = txid } };
            Signer[] signers = _params.Count >= 2 ? ((JArray)_params[1]).Select(j => new Signer() { Account = AddressToScriptHash(j.AsString(), system.Settings.AddressVersion), Scopes = WitnessScope.None }).ToArray() : Array.Empty<Signer>();
            signers.Any().True_Or(RpcErrorFactory.BadRequest("No signer."));
            Transaction tx = new Transaction
            {
                Signers = signers,
                Attributes = conflict,
                Witnesses = Array.Empty<Witness>(),
            };

            tx = Result.Ok_Or(() => wallet.MakeTransaction(system.StoreView, new[] { (byte)OpCode.RET }, signers[0].Account, signers, conflict), RpcError.InsufficientFunds, true);

            if (system.MemPool.TryGetValue(txid, out Transaction conflictTx))
            {
                tx.NetworkFee = Math.Max(tx.NetworkFee, conflictTx.NetworkFee) + 1;
            }
            else if (_params.Count >= 3)
            {
                var extraFee = _params[2].AsString();
                AssetDescriptor descriptor = new(system.StoreView, system.Settings, NativeContract.GAS.Hash);
                (BigDecimal.TryParse(extraFee, descriptor.Decimals, out BigDecimal decimalExtraFee) && decimalExtraFee.Sign > 0).True_Or(RpcErrorFactory.InvalidParams("Incorrect amount format."));

                tx.NetworkFee += (long)decimalExtraFee.Value;
            }
            return SignAndRelay(system.StoreView, tx);
        }
    }
}
