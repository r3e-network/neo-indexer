// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Wallet.Verification.cs file belongs to the neo project and is free
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
using System;
using System.Linq;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Invokes the verify method of a contract.
        /// </summary>
        /// <param name="_params">An array containing the script hash, optional arguments, and optional signers and witnesses.</param>
        /// <returns>A JSON object containing the result of the verification.</returns>
        /// <exception cref="RpcException">Thrown when the script hash is invalid, the contract is not found, or the verification fails.</exception>
        [RpcMethod]
        protected internal virtual JToken InvokeContractVerify(JArray _params)
        {
            UInt160 script_hash = Result.Ok_Or(() => UInt160.Parse(_params[0].AsString()), RpcError.InvalidParams.WithData($"Invalid script hash: {_params[0]}"));
            ContractParameter[] args = _params.Count >= 2 ? ((JArray)_params[1]).Select(p => ContractParameter.FromJson((JObject)p)).ToArray() : Array.Empty<ContractParameter>();
            Signer[] signers = _params.Count >= 3 ? SignersFromJson((JArray)_params[2], system.Settings) : null;
            Witness[] witnesses = _params.Count >= 3 ? WitnessesFromJson((JArray)_params[2]) : null;
            return GetVerificationResult(script_hash, args, signers, witnesses);
        }

        /// <summary>
        /// Gets the result of the contract verification.
        /// </summary>
        /// <param name="scriptHash">The script hash of the contract.</param>
        /// <param name="args">The contract parameters.</param>
        /// <param name="signers">Optional signers for the verification.</param>
        /// <param name="witnesses">Optional witnesses for the verification.</param>
        /// <returns>A JSON object containing the verification result.</returns>
        private JObject GetVerificationResult(UInt160 scriptHash, ContractParameter[] args, Signer[] signers = null, Witness[] witnesses = null)
        {
            using var snapshot = system.GetSnapshotCache();
            var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash).NotNull_Or(RpcError.UnknownContract);
            var md = contract.Manifest.Abi.GetMethod(ContractBasicMethod.Verify, args.Count()).NotNull_Or(RpcErrorFactory.InvalidContractVerification(contract.Hash, args.Count()));
            (md.ReturnType == ContractParameterType.Boolean).True_Or(RpcErrorFactory.InvalidContractVerification("The verify method doesn't return boolean value."));
            Transaction tx = new()
            {
                Signers = signers ?? new Signer[] { new() { Account = scriptHash } },
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = witnesses,
                Script = new[] { (byte)OpCode.RET }
            };
            using ApplicationEngine engine = ApplicationEngine.Create(TriggerType.Verification, tx, snapshot.CloneCache(), settings: system.Settings);
            engine.LoadContract(contract, md, CallFlags.ReadOnly);

            var invocationScript = Array.Empty<byte>();
            if (args.Length > 0)
            {
                using ScriptBuilder sb = new();
                for (int i = args.Length - 1; i >= 0; i--)
                    sb.EmitPush(args[i]);

                invocationScript = sb.ToArray();
                tx.Witnesses ??= new Witness[] { new() { InvocationScript = invocationScript } };
                engine.LoadScript(new Script(invocationScript), configureState: p => p.CallFlags = CallFlags.None);
            }
            JObject json = new();
            json["script"] = Convert.ToBase64String(invocationScript);
            json["state"] = engine.Execute();
            // Gas consumed in the unit of datoshi, 1 GAS = 1e8 datoshi
            json["gasconsumed"] = engine.FeeConsumed.ToString();
            json["exception"] = GetExceptionMessage(engine.FaultException);
            try
            {
                json["stack"] = new JArray(engine.ResultStack.Select(p => p.ToJson(settings.MaxStackSize)));
            }
            catch (Exception ex)
            {
                json["exception"] = ex.Message;
            }
            return json;
        }
    }
}

