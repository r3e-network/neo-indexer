// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.Blockchain.cs file belongs to the neo project and is free
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
        #region Blockchain

        /// <summary>
        /// Returns the hash of the tallest block in the main chain.
        /// </summary>
        public async Task<string> GetBestBlockHashAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the hash of the tallest block in the main chain.
        /// The serialized information of the block is returned, represented by a hexadecimal string.
        /// </summary>
        public async Task<string> GetBlockHexAsync(string hashOrIndex)
        {
            var result = int.TryParse(hashOrIndex, out int index)
                ? await RpcSendAsync(GetRpcName(), index).ConfigureAwait(false)
                : await RpcSendAsync(GetRpcName(), hashOrIndex).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the hash of the tallest block in the main chain.
        /// </summary>
        public async Task<RpcBlock> GetBlockAsync(string hashOrIndex)
        {
            var result = int.TryParse(hashOrIndex, out int index)
                ? await RpcSendAsync(GetRpcName(), index, true).ConfigureAwait(false)
                : await RpcSendAsync(GetRpcName(), hashOrIndex, true).ConfigureAwait(false);

            return RpcBlock.FromJson((JObject)result, protocolSettings);
        }

        /// <summary>
        /// Gets the number of block header in the main chain.
        /// </summary>
        public async Task<uint> GetBlockHeaderCountAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return (uint)result.AsNumber();
        }

        /// <summary>
        /// Gets the number of blocks in the main chain.
        /// </summary>
        public async Task<uint> GetBlockCountAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return (uint)result.AsNumber();
        }

        /// <summary>
        /// Returns the hash value of the corresponding block, based on the specified index.
        /// </summary>
        public async Task<string> GetBlockHashAsync(uint index)
        {
            var result = await RpcSendAsync(GetRpcName(), index).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the corresponding block header information according to the specified script hash.
        /// </summary>
        public async Task<string> GetBlockHeaderHexAsync(string hashOrIndex)
        {
            var result = int.TryParse(hashOrIndex, out int index)
                ? await RpcSendAsync(GetRpcName(), index).ConfigureAwait(false)
                : await RpcSendAsync(GetRpcName(), hashOrIndex).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the corresponding block header information according to the specified script hash.
        /// </summary>
        public async Task<RpcBlockHeader> GetBlockHeaderAsync(string hashOrIndex)
        {
            var result = int.TryParse(hashOrIndex, out int index)
                ? await RpcSendAsync(GetRpcName(), index, true).ConfigureAwait(false)
                : await RpcSendAsync(GetRpcName(), hashOrIndex, true).ConfigureAwait(false);

            return RpcBlockHeader.FromJson((JObject)result, protocolSettings);
        }

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
        /// Obtains the list of unconfirmed transactions in memory.
        /// </summary>
        public async Task<string[]> GetRawMempoolAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return ((JArray)result).Select(p => p.AsString()).ToArray();
        }

        /// <summary>
        /// Obtains the list of unconfirmed transactions in memory.
        /// shouldGetUnverified = true
        /// </summary>
        public async Task<RpcRawMemPool> GetRawMempoolBothAsync()
        {
            var result = await RpcSendAsync(GetRpcName(), true).ConfigureAwait(false);
            return RpcRawMemPool.FromJson((JObject)result);
        }

        /// <summary>
        /// Returns the corresponding transaction information, based on the specified hash value.
        /// </summary>
        public async Task<string> GetRawTransactionHexAsync(string txHash)
        {
            var result = await RpcSendAsync(GetRpcName(), txHash).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the corresponding transaction information, based on the specified hash value.
        /// verbose = true
        /// </summary>
        public async Task<RpcTransaction> GetRawTransactionAsync(string txHash)
        {
            var result = await RpcSendAsync(GetRpcName(), txHash, true).ConfigureAwait(false);
            return RpcTransaction.FromJson((JObject)result, protocolSettings);
        }

        /// <summary>
        /// Calculate network fee
        /// </summary>
        /// <param name="tx">Transaction</param>
        /// <returns>NetworkFee</returns>
        public async Task<long> CalculateNetworkFeeAsync(Transaction tx)
        {
            var json = await RpcSendAsync(GetRpcName(), Convert.ToBase64String(tx.ToArray()))
                .ConfigureAwait(false);
            return (long)json["networkfee"].AsNumber();
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

        /// <summary>
        /// Returns the block index in which the transaction is found.
        /// </summary>
        public async Task<uint> GetTransactionHeightAsync(string txHash)
        {
            var result = await RpcSendAsync(GetRpcName(), txHash).ConfigureAwait(false);
            return uint.Parse(result.AsString());
        }

        /// <summary>
        /// Returns the next NEO consensus nodes information and voting status.
        /// </summary>
        public async Task<RpcValidator[]> GetNextBlockValidatorsAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return ((JArray)result).Select(p => RpcValidator.FromJson((JObject)p)).ToArray();
        }

        /// <summary>
        /// Returns the current NEO committee members.
        /// </summary>
        public async Task<string[]> GetCommitteeAsync()
        {
            var result = await RpcSendAsync(GetRpcName()).ConfigureAwait(false);
            return ((JArray)result).Select(p => p.AsString()).ToArray();
        }

        #endregion Blockchain
    }
}
