// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.Traces.Parsing.Helpers.cs file belongs to the neo project and is free
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
using System;
using System.Globalization;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        private static string? ParseTransactionHashFilter(JObject obj)
        {
            if (!obj.ContainsProperty("transactionHash"))
                return null;
            var raw = obj["transactionHash"]?.AsString();
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            if (!UInt256.TryParse(raw, out var hash))
                throw new RpcException(RpcError.InvalidParams.WithData($"invalid transaction hash: {raw}"));
            return hash.ToString();
        }

        private static int NormalizeLimit(int? value)
        {
            if (!value.HasValue)
                return DefaultTraceLimit;
            if (value.Value <= 0)
                throw new RpcException(RpcError.InvalidParams.WithData("limit must be positive"));
            return Math.Min(value.Value, MaxTraceLimit);
        }

        private static int NormalizeOffset(int? value)
        {
            if (!value.HasValue)
                return 0;
            return value.Value < 0 ? 0 : value.Value;
        }

        private static int? TryParseInt(JObject obj, string propertyName)
        {
            if (!obj.ContainsProperty(propertyName))
                return null;
            var token = obj[propertyName];
            if (token is null)
                return null;
            var raw = token.AsString();
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            throw new RpcException(RpcError.InvalidParams.WithData($"invalid value for {propertyName}: {raw}"));
        }

        private static uint? TryParseUInt(JObject obj, string propertyName)
        {
            if (!obj.ContainsProperty(propertyName))
                return null;
            var token = obj[propertyName];
            if (token is null)
                return null;
            var raw = token.AsString();
            if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            throw new RpcException(RpcError.InvalidParams.WithData($"invalid value for {propertyName}: {raw}"));
        }

        private static uint? ParseUIntParam(JToken? token, string parameterName)
        {
            if (token is null)
                return null;

            var raw = token.AsString();
            if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            throw new RpcException(RpcError.InvalidParams.WithData($"invalid value for {parameterName}: {raw}"));
        }
    }
}

