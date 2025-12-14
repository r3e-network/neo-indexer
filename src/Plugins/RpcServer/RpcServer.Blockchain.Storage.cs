// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Blockchain.Storage.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Extensions;
using Neo.Json;
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.Linq;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Gets the storage item by contract ID or script hash and key.
        /// </summary>
        /// <param name="contractNameOrHashOrId">The contract ID or script hash.</param>
        /// <param name="base64Key">The Base64-encoded storage key.</param>
        /// <returns>The storage item as a <see cref="JToken"/>.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken GetStorage(ContractNameOrHashOrId contractNameOrHashOrId, string base64Key)
        {
            using var snapshot = system.GetSnapshotCache();
            int id;
            if (contractNameOrHashOrId.IsHash)
            {
                var hash = contractNameOrHashOrId.AsHash();
                var contract = NativeContract.ContractManagement.GetContract(snapshot, hash).NotNull_Or(RpcError.UnknownContract);
                id = contract.Id;
            }
            else
            {
                id = contractNameOrHashOrId.AsId();
            }
            var key = Convert.FromBase64String(base64Key);
            var item = snapshot.TryGet(new StorageKey
            {
                Id = id,
                Key = key
            }).NotNull_Or(RpcError.UnknownStorageItem);
            return Convert.ToBase64String(item.Value.Span);
        }

        /// <summary>
        /// Finds storage items by contract ID or script hash and prefix.
        /// </summary>
        /// <param name="contractNameOrHashOrId">The contract ID (int) or script hash (UInt160).</param>
        /// <param name="base64KeyPrefix">The Base64-encoded storage key prefix.</param>
        /// <param name="start">The start index.</param>
        /// <returns>The found storage items <see cref="StorageItem"/> as a <see cref="JToken"/>.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken FindStorage(ContractNameOrHashOrId contractNameOrHashOrId, string base64KeyPrefix, int start = 0)
        {
            using var snapshot = system.GetSnapshotCache();
            int id;
            if (contractNameOrHashOrId.IsHash)
            {
                ContractState contract = NativeContract.ContractManagement.GetContract(snapshot, contractNameOrHashOrId.AsHash()).NotNull_Or(RpcError.UnknownContract);
                id = contract.Id;
            }
            else
            {
                id = contractNameOrHashOrId.AsId();
            }

            byte[] prefix = Result.Ok_Or(() => Convert.FromBase64String(base64KeyPrefix), RpcError.InvalidParams.WithData($"Invalid Base64 string{base64KeyPrefix}"));

            JObject json = new();
            JArray jarr = new();
            int pageSize = settings.FindStoragePageSize;
            int i = 0;

            using (var iter = NativeContract.ContractManagement.FindContractStorage(snapshot, id, prefix).Skip(count: start).GetEnumerator())
            {
                var hasMore = false;
                while (iter.MoveNext())
                {
                    if (i == pageSize)
                    {
                        hasMore = true;
                        break;
                    }

                    JObject j = new();
                    j["key"] = Convert.ToBase64String(iter.Current.Key.Key.Span);
                    j["value"] = Convert.ToBase64String(iter.Current.Value.Value.Span);
                    jarr.Add(j);
                    i++;
                }
                json["truncated"] = hasMore;
            }

            json["next"] = start + i;
            json["results"] = jarr;
            return json;
        }
    }
}
