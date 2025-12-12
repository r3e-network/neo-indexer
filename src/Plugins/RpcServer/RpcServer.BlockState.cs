// Copyright (C) 2015-2025 The Neo Project.
//
// RpcServer.BlockState.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.Persistence;
using Neo.SmartContract.Native;
using System;

namespace Neo.Plugins.RpcServer
{
    partial class RpcServer
    {
        /// <summary>
        /// Returns download metadata for the recorded initial storage reads of a block.
        /// </summary>
        /// <param name="height">Block height.</param>
        /// <param name="format">Export format: bin (NSBR), json, or csv.</param>
        /// <remarks>
        /// This assumes the state recorder is enabled and uploading to Supabase Storage.
        /// </remarks>
        [RpcMethodWithParams]
        protected internal virtual JToken GetBlockStateExport(uint height, string format = "bin")
        {
            var settings = StateRecorderSettings.Current;
            if (!settings.Enabled)
                throw new RpcException(RpcError.InternalServerError.WithData("state recorder not enabled"));

            var fmt = format.ToLowerInvariant();
            if (fmt is not ("bin" or "json" or "csv"))
                throw new RpcException(RpcError.InvalidParams.WithData("format must be \"bin\", \"json\", or \"csv\""));

            var isBinaryMode = settings.Mode is StateRecorderSettings.UploadMode.Binary or StateRecorderSettings.UploadMode.Both;
            if (!settings.UploadEnabled || !isBinaryMode)
                throw new RpcException(RpcError.InternalServerError.WithData("state recorder uploads not enabled"));

            var path = $"block-{height}.{fmt}";
            var bucket = settings.SupabaseBucket;
            var baseUrl = settings.SupabaseUrl?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new RpcException(RpcError.InternalServerError.WithData("supabase url not configured"));

            string publicUrl = $"{baseUrl}/storage/v1/object/public/{bucket}/{path}";
            string authUrl = $"{baseUrl}/storage/v1/object/{bucket}/{path}";

            var snapshot = system.StoreView;
            var hash = NativeContract.Ledger.GetBlockHash(snapshot, height) ?? throw new RpcException(RpcError.UnknownBlock);
            var json = new JObject();
            json["height"] = height;
            json["hash"] = hash.ToString();
            json["path"] = path;
            json["bucket"] = bucket;
            json["publicUrl"] = publicUrl;
            json["authUrl"] = authUrl;
            json["note"] = "publicUrl assumes bucket is public; use authUrl with service key if private.";
            json["format"] = fmt;
            return json;
        }
    }
}
