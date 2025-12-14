// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.Blockchain.Blocks.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Network.RPC.Models;
using System;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    public partial class RpcClient
    {
        #region Blockchain - Blocks

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

        #endregion Blockchain - Blocks
    }
}
