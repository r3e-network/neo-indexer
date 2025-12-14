// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.SmartContract.cs file belongs to the neo project and is free
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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    public partial class RpcClient
    {
        #region SmartContract

        /// <summary>
        /// Returns the result after calling a smart contract at scripthash with the given operation and parameters.
        /// This RPC call does not affect the blockchain in any way.
        /// </summary>
        public async Task<RpcInvokeResult> InvokeFunctionAsync(string scriptHash, string operation, RpcStack[] stacks, params Signer[] signer)
        {
            List<JToken> parameters = new() { scriptHash.AsScriptHash(), operation, stacks.Select(p => p.ToJson()).ToArray() };
            if (signer.Length > 0)
            {
                parameters.Add(signer.Select(p => p.ToJson()).ToArray());
            }
            var result = await RpcSendAsync(GetRpcName(), parameters.ToArray()).ConfigureAwait(false);
            return RpcInvokeResult.FromJson((JObject)result);
        }

        /// <summary>
        /// Returns the result after passing a script through the VM.
        /// This RPC call does not affect the blockchain in any way.
        /// </summary>
        public async Task<RpcInvokeResult> InvokeScriptAsync(ReadOnlyMemory<byte> script, params Signer[] signers)
        {
            List<JToken> parameters = new() { Convert.ToBase64String(script.Span) };
            if (signers.Length > 0)
            {
                parameters.Add(signers.Select(p => p.ToJson()).ToArray());
            }
            var result = await RpcSendAsync(GetRpcName(), parameters.ToArray()).ConfigureAwait(false);
            return RpcInvokeResult.FromJson((JObject)result);
        }

        public async Task<RpcUnclaimedGas> GetUnclaimedGasAsync(string address)
        {
            var result = await RpcSendAsync(GetRpcName(), address.AsScriptHash()).ConfigureAwait(false);
            return RpcUnclaimedGas.FromJson((JObject)result);
        }


        public async IAsyncEnumerable<JObject> TraverseIteratorAsync(string sessionId, string id)
        {
            const int count = 100;
            while (true)
            {
                var result = await RpcSendAsync(GetRpcName(), sessionId, id, count).ConfigureAwait(false);
                var array = (JArray)result;
                foreach (JObject jObject in array)
                {
                    yield return jObject;
                }
                if (array.Count < count) break;
            }
        }

        /// <summary>
        /// Returns limit <paramref name="count"/> results from Iterator.
        /// This RPC call does not affect the blockchain in any way.
        /// </summary>
        /// <param name="sessionId"></param>
        /// <param name="id"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public async IAsyncEnumerable<JObject> TraverseIteratorAsync(string sessionId, string id, int count)
        {
            var result = await RpcSendAsync(GetRpcName(), sessionId, id, count).ConfigureAwait(false);
            if (result is JArray { Count: > 0 } array)
            {
                foreach (JObject jObject in array)
                {
                    yield return jObject;
                }
            }
        }

        /// <summary>
        /// Terminate specified Iterator session.
        /// This RPC call does not affect the blockchain in any way.
        /// </summary>
        public async Task<bool> TerminateSessionAsync(string sessionId)
        {
            var result = await RpcSendAsync(GetRpcName(), sessionId).ConfigureAwait(false);
            return result.GetBoolean();
        }

        #endregion SmartContract
    }
}
