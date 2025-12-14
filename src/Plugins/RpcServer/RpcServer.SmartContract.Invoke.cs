// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.SmartContract.Invoke.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Extensions;
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
        private JObject GetInvokeResult(byte[] script, Signer[] signers = null, Witness[] witnesses = null, bool useDiagnostic = false)
        {
            JObject json = new();
            Session session = new(system, script, signers, witnesses, settings.MaxGasInvoke, useDiagnostic ? new Diagnostic() : null);
            try
            {
                json["script"] = Convert.ToBase64String(script);
                json["state"] = session.Engine.State;
                // Gas consumed in the unit of datoshi, 1 GAS = 10^8 datoshi
                json["gasconsumed"] = session.Engine.FeeConsumed.ToString();
                json["exception"] = GetExceptionMessage(session.Engine.FaultException);
                json["notifications"] = new JArray(session.Engine.Notifications.Select(n =>
                {
                    var obj = new JObject();
                    obj["eventname"] = n.EventName;
                    obj["contract"] = n.ScriptHash.ToString();
                    obj["state"] = ToJson(n.State, session);
                    return obj;
                }));
                if (useDiagnostic)
                {
                    Diagnostic diagnostic = (Diagnostic)session.Engine.Diagnostic;
                    json["diagnostics"] = new JObject()
                    {
                        ["invokedcontracts"] = ToJson(diagnostic.InvocationTree.Root),
                        ["storagechanges"] = ToJson(session.Engine.SnapshotCache.GetChangeSet())
                    };
                }
                var stack = new JArray();
                foreach (var item in session.Engine.ResultStack)
                {
                    try
                    {
                        stack.Add(ToJson(item, session));
                    }
                    catch (Exception ex)
                    {
                        stack.Add("error: " + ex.Message);
                    }
                }
                json["stack"] = stack;
                if (session.Engine.State != VMState.FAULT)
                {
                    ProcessInvokeWithWallet(json, signers);
                }
            }
            catch
            {
                session.Dispose();
                throw;
            }
            if (session.Iterators.Count == 0 || !settings.SessionEnabled)
            {
                session.Dispose();
            }
            else
            {
                Guid id = Guid.NewGuid();
                json["session"] = id.ToString();
                lock (sessions)
                    sessions.Add(id, session);
            }
            return json;
        }

        [RpcMethod]
        protected internal virtual JToken InvokeFunction(JArray _params)
        {
            UInt160 script_hash = Result.Ok_Or(() => UInt160.Parse(_params[0].AsString()), RpcError.InvalidParams.WithData($"Invalid script hash {nameof(script_hash)}"));
            string operation = Result.Ok_Or(() => _params[1].AsString(), RpcError.InvalidParams);
            ContractParameter[] args = _params.Count >= 3 ? ((JArray)_params[2]).Select(p => ContractParameter.FromJson((JObject)p)).ToArray() : [];
            Signer[] signers = _params.Count >= 4 ? SignersFromJson((JArray)_params[3], system.Settings) : null;
            Witness[] witnesses = _params.Count >= 4 ? WitnessesFromJson((JArray)_params[3]) : null;
            bool useDiagnostic = _params.Count >= 5 && _params[4].GetBoolean();

            byte[] script;
            using (ScriptBuilder sb = new())
            {
                script = sb.EmitDynamicCall(script_hash, operation, args).ToArray();
            }
            return GetInvokeResult(script, signers, witnesses, useDiagnostic);
        }

        [RpcMethod]
        protected internal virtual JToken InvokeScript(JArray _params)
        {
            byte[] script = Result.Ok_Or(() => Convert.FromBase64String(_params[0].AsString()), RpcError.InvalidParams);
            Signer[] signers = _params.Count >= 2 ? SignersFromJson((JArray)_params[1], system.Settings) : null;
            Witness[] witnesses = _params.Count >= 2 ? WitnessesFromJson((JArray)_params[1]) : null;
            bool useDiagnostic = _params.Count >= 3 && _params[2].GetBoolean();
            return GetInvokeResult(script, signers, witnesses, useDiagnostic);
        }

        [RpcMethod]
        protected internal virtual JToken GetUnclaimedGas(JArray _params)
        {
            string address = Result.Ok_Or(() => _params[0].AsString(), RpcError.InvalidParams.WithData($"Invalid address {nameof(address)}"));
            JObject json = new();
            UInt160 script_hash = Result.Ok_Or(() => AddressToScriptHash(address, system.Settings.AddressVersion), RpcError.InvalidParams);

            var snapshot = system.StoreView;
            json["unclaimed"] = NativeContract.NEO.UnclaimedGas(snapshot, script_hash, NativeContract.Ledger.CurrentIndex(snapshot) + 1).ToString();
            json["address"] = script_hash.ToAddress(system.Settings.AddressVersion);
            return json;
        }

        static string GetExceptionMessage(Exception exception)
        {
            if (exception == null) return null;

            // First unwrap any TargetInvocationException
            var unwrappedException = UnwrapException(exception);

            // Then get the base exception message
            return unwrappedException.GetBaseException().Message;
        }
    }
}
