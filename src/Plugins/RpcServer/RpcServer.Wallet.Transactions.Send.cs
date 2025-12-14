// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Wallet.Transactions.Send.cs file belongs to the neo project and is free
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
using Neo.Wallets;
using System.Linq;
using System.Numerics;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Transfers an asset from a specific address to another address.
        /// </summary>
        /// <param name="_params">An array containing asset ID, from address, to address, amount, and optional signers.</param>
        /// <returns>The transaction details if successful, or the contract parameters if signatures are incomplete.</returns>
        /// <exception cref="RpcException">Thrown when no wallet is open, parameters are invalid, or there are insufficient funds.</exception>
        [RpcMethod]
        protected internal virtual JToken SendFrom(JArray _params)
        {
            CheckWallet();
            UInt160 assetId = Result.Ok_Or(() => UInt160.Parse(_params[0].AsString()), RpcError.InvalidParams.WithData($"Invalid asset id: {_params[0]}"));
            UInt160 from = AddressToScriptHash(_params[1].AsString(), system.Settings.AddressVersion);
            UInt160 to = AddressToScriptHash(_params[2].AsString(), system.Settings.AddressVersion);
            using var snapshot = system.GetSnapshotCache();
            AssetDescriptor descriptor = new(snapshot, system.Settings, assetId);
            BigDecimal amount = new(BigInteger.Parse(_params[3].AsString()), descriptor.Decimals);
            (amount.Sign > 0).True_Or(RpcErrorFactory.InvalidParams("Amount can't be negative."));
            Signer[] signers = _params.Count >= 5 ? ((JArray)_params[4]).Select(p => new Signer() { Account = AddressToScriptHash(p.AsString(), system.Settings.AddressVersion), Scopes = WitnessScope.CalledByEntry }).ToArray() : null;

            Transaction tx = Result.Ok_Or(() => wallet.MakeTransaction(snapshot, new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = to
                }
            }, from, signers), RpcError.InvalidRequest.WithData("Can not process this request.")).NotNull_Or(RpcError.InsufficientFunds);

            ContractParametersContext transContext = new(snapshot, tx, settings.Network);
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * NativeContract.Policy.GetFeePerByte(snapshot) + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            (tx.NetworkFee <= settings.MaxFee).True_Or(RpcError.WalletFeeLimit);
            return SignAndRelay(snapshot, tx);
        }

        /// <summary>
        /// Transfers assets to multiple addresses.
        /// </summary>
        /// <param name="_params">
        /// An array containing the following elements:
        /// [0] (optional): The address to send from as a string. If omitted, the assets will be sent from any address in the wallet.
        /// [1]: An array of transfer objects, each containing:
        ///     - "asset": The asset ID (UInt160) as a string.
        ///     - "value": The amount to transfer as a string.
        ///     - "address": The recipient address as a string.
        /// [2] (optional): An array of signers, each containing:
        ///     - The address of the signer as a string.
        /// </param>
        /// <returns>
        /// If the transaction is successfully created and all signatures are present:
        ///     Returns a JSON object representing the transaction.
        /// If not all signatures are present:
        ///     Returns a JSON object representing the contract parameters that need to be signed.
        /// </returns>
        /// <exception cref="RpcException">
        /// Thrown when:
        /// - No wallet is open.
        /// - The 'to' parameter is invalid or empty.
        /// - Any of the asset IDs are invalid.
        /// - Any of the amounts are negative or invalid.
        /// - Any of the addresses are invalid.
        /// - There are insufficient funds for the transfer.
        /// - The network fee exceeds the maximum allowed fee.
        /// </exception>
        [RpcMethod]
        protected internal virtual JToken SendMany(JArray _params)
        {
            CheckWallet();
            int to_start = 0;
            UInt160 from = null;
            if (_params[0] is JString)
            {
                from = AddressToScriptHash(_params[0].AsString(), system.Settings.AddressVersion);
                to_start = 1;
            }
            JArray to = Result.Ok_Or(() => (JArray)_params[to_start], RpcError.InvalidParams.WithData($"Invalid 'to' parameter: {_params[to_start]}"));
            (to.Count != 0).True_Or(RpcErrorFactory.InvalidParams("Argument 'to' can't be empty."));
            Signer[] signers = _params.Count >= to_start + 2 ? ((JArray)_params[to_start + 1]).Select(p => new Signer() { Account = AddressToScriptHash(p.AsString(), system.Settings.AddressVersion), Scopes = WitnessScope.CalledByEntry }).ToArray() : null;

            TransferOutput[] outputs = new TransferOutput[to.Count];
            using var snapshot = system.GetSnapshotCache();
            for (int i = 0; i < to.Count; i++)
            {
                UInt160 asset_id = UInt160.Parse(to[i]["asset"].AsString());
                AssetDescriptor descriptor = new(snapshot, system.Settings, asset_id);
                outputs[i] = new TransferOutput
                {
                    AssetId = asset_id,
                    Value = new BigDecimal(BigInteger.Parse(to[i]["value"].AsString()), descriptor.Decimals),
                    ScriptHash = AddressToScriptHash(to[i]["address"].AsString(), system.Settings.AddressVersion)
                };
                (outputs[i].Value.Sign > 0).True_Or(RpcErrorFactory.InvalidParams($"Amount of '{asset_id}' can't be negative."));
            }
            Transaction tx = wallet.MakeTransaction(snapshot, outputs, from, signers).NotNull_Or(RpcError.InsufficientFunds);

            ContractParametersContext transContext = new(snapshot, tx, settings.Network);
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * NativeContract.Policy.GetFeePerByte(snapshot) + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            (tx.NetworkFee <= settings.MaxFee).True_Or(RpcError.WalletFeeLimit);
            return SignAndRelay(snapshot, tx);
        }

        /// <summary>
        /// Transfers an asset to a specific address.
        /// </summary>
        /// <param name="_params">An array containing asset ID, to address, and amount.</param>
        /// <returns>The transaction details if successful, or the contract parameters if signatures are incomplete.</returns>
        /// <exception cref="RpcException">Thrown when no wallet is open, parameters are invalid, or there are insufficient funds.</exception>
        [RpcMethod]
        protected internal virtual JToken SendToAddress(JArray _params)
        {
            CheckWallet();
            UInt160 assetId = Result.Ok_Or(() => UInt160.Parse(_params[0].AsString()), RpcError.InvalidParams.WithData($"Invalid asset hash: {_params[0]}"));
            UInt160 to = AddressToScriptHash(_params[1].AsString(), system.Settings.AddressVersion);
            using var snapshot = system.GetSnapshotCache();
            AssetDescriptor descriptor = new(snapshot, system.Settings, assetId);
            BigDecimal amount = new(BigInteger.Parse(_params[2].AsString()), descriptor.Decimals);
            (amount.Sign > 0).True_Or(RpcError.InvalidParams);
            Transaction tx = wallet.MakeTransaction(snapshot, new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = to
                }
            }).NotNull_Or(RpcError.InsufficientFunds);

            ContractParametersContext transContext = new(snapshot, tx, settings.Network);
            wallet.Sign(transContext);
            if (!transContext.Completed)
                return transContext.ToJson();
            tx.Witnesses = transContext.GetWitnesses();
            if (tx.Size > 1024)
            {
                long calFee = tx.Size * NativeContract.Policy.GetFeePerByte(snapshot) + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }
            (tx.NetworkFee <= settings.MaxFee).True_Or(RpcError.WalletFeeLimit);
            return SignAndRelay(snapshot, tx);
        }
    }
}
