// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Blockchain.Blocks.cs file belongs to the neo project and is free
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
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract.Native;
using System;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Gets the hash of the best (most recent) block.
        /// </summary>
        /// <returns>The hash of the best block as a <see cref="JToken"/>.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken GetBestBlockHash()
        {
            return NativeContract.Ledger.CurrentHash(system.StoreView).ToString();
        }

        /// <summary>
        /// Gets a block by its hash or index.
        /// </summary>
        /// <param name="blockHashOrIndex">The block hash or index.</param>
        /// <param name="verbose">Optional, the default value is false.</param>
        /// <returns>The block data as a <see cref="JToken"/>. If the second item of _params is true, then
        /// block data is json format, otherwise, the return type is Base64-encoded byte array.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken GetBlock(BlockHashOrIndex blockHashOrIndex, bool verbose = false)
        {
            using var snapshot = system.GetSnapshotCache();
            var block = blockHashOrIndex.IsIndex
                ? NativeContract.Ledger.GetBlock(snapshot, blockHashOrIndex.AsIndex())
                : NativeContract.Ledger.GetBlock(snapshot, blockHashOrIndex.AsHash());
            block.NotNull_Or(RpcError.UnknownBlock);
            if (verbose)
            {
                JObject json = Utility.BlockToJson(block, system.Settings);
                json["confirmations"] = NativeContract.Ledger.CurrentIndex(snapshot) - block.Index + 1;
                UInt256 hash = NativeContract.Ledger.GetBlockHash(snapshot, block.Index + 1);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();
                return json;
            }
            return Convert.ToBase64String(block.ToArray());
        }

        /// <summary>
        /// Gets the number of block headers in the blockchain.
        /// </summary>
        /// <returns>The count of block headers as a <see cref="JToken"/>.</returns>
        [RpcMethodWithParams]
        internal virtual JToken GetBlockHeaderCount()
        {
            return (system.HeaderCache.Last?.Index ?? NativeContract.Ledger.CurrentIndex(system.StoreView)) + 1;
        }

        /// <summary>
        /// Gets the number of blocks in the blockchain.
        /// </summary>
        /// <returns>The count of blocks as a <see cref="JToken"/>.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken GetBlockCount()
        {
            return NativeContract.Ledger.CurrentIndex(system.StoreView) + 1;
        }

        /// <summary>
        /// Gets the hash of the block at the specified height.
        /// </summary>
        /// <param name="height">Block index (block height)</param>
        /// <returns>The hash of the block at the specified height as a <see cref="JToken"/>.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken GetBlockHash(uint height)
        {
            var snapshot = system.StoreView;
            if (height <= NativeContract.Ledger.CurrentIndex(snapshot))
            {
                return NativeContract.Ledger.GetBlockHash(snapshot, height).ToString();
            }
            throw new RpcException(RpcError.UnknownHeight);
        }

        /// <summary>
        /// Gets a block header by its hash or index.
        /// </summary>
        /// <param name="blockHashOrIndex">The block script hash or index (i.e. block height=number of blocks - 1).</param>
        /// <param name="verbose">Optional, the default value is false.</param>
        /// <remarks>
        /// When verbose is false, serialized information of the block is returned in a hexadecimal string.
        /// If you need the detailed information, use the SDK for deserialization.
        /// When verbose is true or 1, detailed information of the block is returned in Json format.
        /// </remarks>
        /// <returns>
        /// The block header data as a <see cref="JToken"/>.
        /// In json format if the second item of _params is true, otherwise Base64-encoded byte array.
        /// </returns>
        [RpcMethodWithParams]
        protected internal virtual JToken GetBlockHeader(BlockHashOrIndex blockHashOrIndex, bool verbose = false)
        {
            var snapshot = system.StoreView;
            Header header;
            if (blockHashOrIndex.IsIndex)
            {
                header = NativeContract.Ledger.GetHeader(snapshot, blockHashOrIndex.AsIndex()).NotNull_Or(RpcError.UnknownBlock);
            }
            else
            {
                header = NativeContract.Ledger.GetHeader(snapshot, blockHashOrIndex.AsHash()).NotNull_Or(RpcError.UnknownBlock);
            }
            if (verbose)
            {
                JObject json = header.ToJson(system.Settings);
                json["confirmations"] = NativeContract.Ledger.CurrentIndex(snapshot) - header.Index + 1;
                UInt256 hash = NativeContract.Ledger.GetBlockHash(snapshot, header.Index + 1);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();
                return json;
            }

            return Convert.ToBase64String(header.ToArray());
        }
    }
}

