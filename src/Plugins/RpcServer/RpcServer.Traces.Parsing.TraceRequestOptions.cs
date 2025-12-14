// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Parsing.TraceRequestOptions.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Json;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private static TraceRequestOptions ParseTraceRequestOptions(JToken? token, bool allowTransactionFilter)
        {
            var options = new TraceRequestOptions();
            if (token is not JObject obj)
                return options;

            options.Limit = NormalizeLimit(TryParseInt(obj, "limit"));
            options.Offset = NormalizeOffset(TryParseInt(obj, "offset"));
            if (allowTransactionFilter)
                options.TransactionHash = ParseTransactionHashFilter(obj);

            return options;
        }
    }
}

