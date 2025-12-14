// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.Blockchain.Contracts.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    public partial class RpcClient
    {
        #region Blockchain - Contracts

        /// <summary>
        /// Queries contract information, according to the contract script hash.
        /// </summary>
        public async Task<ContractState> GetContractStateAsync(string hash)
        {
            var result = await RpcSendAsync(GetRpcName(), hash).ConfigureAwait(false);
            return ContractStateFromJson((JObject)result);
        }

        /// <summary>
        /// Queries contract information, according to the contract id.
        /// </summary>
        public async Task<ContractState> GetContractStateAsync(int id)
        {
            var result = await RpcSendAsync(GetRpcName(), id).ConfigureAwait(false);
            return ContractStateFromJson((JObject)result);
        }

        public static ContractState ContractStateFromJson(JObject json)
        {
            return new ContractState
            {
                Id = (int)json["id"].AsNumber(),
                UpdateCounter = (ushort)(json["updatecounter"]?.AsNumber() ?? 0),
                Hash = UInt160.Parse(json["hash"].AsString()),
                Nef = RpcNefFile.FromJson((JObject)json["nef"]),
                Manifest = ContractManifest.FromJson((JObject)json["manifest"])
            };
        }

        /// <summary>
        /// Get all native contracts.
        /// </summary>
        public async Task<ContractState[]> GetNativeContractsAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return ((JArray)result).Select(p => ContractStateFromJson((JObject)p)).ToArray();
        }

        /// <summary>
        /// Returns the stored value, according to the contract script hash (or Id) and the stored key.
        /// </summary>
        public async Task<string> GetStorageAsync(string scriptHashOrId, string key)
        {
            var result = int.TryParse(scriptHashOrId, out int id)
                ? await RpcSendAsync(GetRpcName(), id, key).ConfigureAwait(false)
                : await RpcSendAsync(GetRpcName(), scriptHashOrId, key).ConfigureAwait(false);
            return result.AsString();
        }

        #endregion Blockchain - Contracts
    }
}
