// Copyright (C) 2015-2025 The Neo Project.
//
// NotificationTraceResult.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Json;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neo.Plugins.RpcServer.Model
{
    /// <summary>
    /// Represents a notifications trace row from Supabase.
    /// </summary>
    public sealed class NotificationTraceResult
    {
        [JsonPropertyName("block_index")]
        public int BlockIndex { get; set; }

        [JsonPropertyName("tx_hash")]
        public string TransactionHash { get; set; } = string.Empty;

        [JsonPropertyName("notification_order")]
        public int NotificationOrder { get; set; }

        [JsonPropertyName("contract_hash")]
        public string ContractHash { get; set; } = string.Empty;

        [JsonPropertyName("event_name")]
        public string EventName { get; set; } = string.Empty;

        [JsonPropertyName("state_json")]
        public JsonElement? StateJson { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["blockIndex"] = BlockIndex;
            json["transactionHash"] = TransactionHash;
            json["notificationOrder"] = NotificationOrder;
            json["contractHash"] = ContractHash;
            json["eventName"] = EventName;

            var state = TryParseState(StateJson);
            if (state is not null)
                json["state"] = state;

            return json;
        }

        private static JToken? TryParseState(JsonElement? element)
        {
            if (!element.HasValue)
                return null;

            try
            {
                var raw = element.Value.GetRawText();
                if (string.IsNullOrWhiteSpace(raw))
                    return null;
                return JToken.Parse(raw);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

