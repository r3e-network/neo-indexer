// Copyright (C) 2015-2025 The Neo Project.
//
// RpcClient.Blockchain.Validators.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Network.RPC.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    public partial class RpcClient
    {
        #region Blockchain - Validators

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

        #endregion Blockchain - Validators
    }
}
