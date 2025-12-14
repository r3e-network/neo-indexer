// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Blockchain.Contracts.cs file belongs to the neo project and is free
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
using Neo.SmartContract.Native;
using System;
using System.Linq;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Gets the state of a contract by its ID or script hash or (only for native contracts) by case-insensitive name.
        /// </summary>
        /// <param name="contractNameOrHashOrId">Contract name or script hash or the native contract id.</param>
        /// <returns>The contract state in json format as a <see cref="JToken"/>.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken GetContractState(ContractNameOrHashOrId contractNameOrHashOrId)
        {
            if (contractNameOrHashOrId.IsId)
            {
                var contractState = NativeContract.ContractManagement.GetContractById(system.StoreView, contractNameOrHashOrId.AsId());
                return contractState.NotNull_Or(RpcError.UnknownContract).ToJson();
            }

            var hash = contractNameOrHashOrId.IsName ? ToScriptHash(contractNameOrHashOrId.AsName()) : contractNameOrHashOrId.AsHash();
            var contract = NativeContract.ContractManagement.GetContract(system.StoreView, hash);
            return contract.NotNull_Or(RpcError.UnknownContract).ToJson();
        }

        private static UInt160 ToScriptHash(string keyword)
        {
            foreach (var native in NativeContract.Contracts)
            {
                if (keyword.Equals(native.Name, StringComparison.InvariantCultureIgnoreCase) || keyword == native.Id.ToString())
                    return native.Hash;
            }

            return UInt160.Parse(keyword);
        }

        /// <summary>
        /// Gets the list of native contracts.
        /// </summary>
        /// <returns>The native contract states <see cref="Neo.SmartContract.ContractState"/> as a <see cref="JToken"/>.</returns>
        [RpcMethodWithParams]
        protected internal virtual JToken GetNativeContracts()
        {
            var storeView = system.StoreView;
            var contractStates = NativeContract.Contracts
                .Select(p => NativeContract.ContractManagement.GetContract(storeView, p.Hash))
                .Where(p => p != null) // if not active
                .Select(p => p.ToJson());
            return new JArray(contractStates);
        }
    }
}
