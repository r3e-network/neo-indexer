// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Parsing.BlockIdentifier.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo;
using Neo.Extensions;
using Neo.Json;
using Neo.Plugins.RpcServer.Model;
using System;
using System.Globalization;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private static BlockHashOrIndex ParseBlockIdentifier(JToken token)
        {
            if (token is null)
                throw new RpcException(RpcError.InvalidParams.WithData("block hash or index is required"));

            if (token is JNumber)
            {
                var number = token.AsNumber();
                if (double.IsNaN(number) || number < 0 || number > uint.MaxValue)
                    throw new RpcException(RpcError.InvalidParams.WithData($"invalid block index: {token}"));
                if (Math.Abs(number % 1) > double.Epsilon)
                    throw new RpcException(RpcError.InvalidParams.WithData("block index must be an integer"));
                return new BlockHashOrIndex((uint)number);
            }

            var raw = token.AsString();
            if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                return new BlockHashOrIndex(index);
            if (UInt256.TryParse(raw, out var hash))
                return new BlockHashOrIndex(hash);
            throw new RpcException(RpcError.InvalidParams.WithData($"invalid block hash or index: {raw}"));
        }

        private static (UInt256 Hash, string HashString) ParseTransactionHash(JToken token, string parameterName)
        {
            var raw = token?.AsString() ?? string.Empty;
            if (!UInt256.TryParse(raw, out var hash))
                throw new RpcException(RpcError.InvalidParams.WithData($"invalid {parameterName}: {raw}"));
            return (hash, hash.ToString());
        }
    }
}

