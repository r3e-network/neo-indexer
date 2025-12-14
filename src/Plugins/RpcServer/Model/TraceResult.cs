// Copyright (C) 2015-2025 The Neo Project.
//
// TraceResult.cs file belongs to the neo project and is free
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
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins.RpcServer.Model
{
    /// <summary>
    /// Aggregated trace payload returned by trace RPC endpoints.
    /// </summary>
    public sealed class TraceResult
    {
        public uint BlockIndex { get; init; }
        public string? BlockHash { get; init; }
        public string? TransactionHash { get; init; }
        public IReadOnlyList<TransactionResultResult> TransactionResults { get; init; } = Array.Empty<TransactionResultResult>();
        public IReadOnlyList<OpCodeTraceResult> OpCodeTraces { get; init; } = Array.Empty<OpCodeTraceResult>();
        public IReadOnlyList<SyscallTraceResult> SyscallTraces { get; init; } = Array.Empty<SyscallTraceResult>();
        public IReadOnlyList<ContractCallResult> ContractCalls { get; init; } = Array.Empty<ContractCallResult>();
        public IReadOnlyList<StorageReadTraceResult> StorageReads { get; init; } = Array.Empty<StorageReadTraceResult>();
        public IReadOnlyList<StorageWriteTraceResult> StorageWrites { get; init; } = Array.Empty<StorageWriteTraceResult>();
        public IReadOnlyList<NotificationTraceResult> Notifications { get; init; } = Array.Empty<NotificationTraceResult>();
        public IReadOnlyList<RuntimeLogTraceResult> Logs { get; init; } = Array.Empty<RuntimeLogTraceResult>();
        public int Limit { get; init; }
        public int Offset { get; init; }
        public int TransactionResultTotal { get; init; }
        public int OpCodeTotal { get; init; }
        public int SyscallTotal { get; init; }
        public int ContractCallTotal { get; init; }
        public int StorageReadTotal { get; init; }
        public int StorageWriteTotal { get; init; }
        public int NotificationTotal { get; init; }
        public int LogTotal { get; init; }

        public JObject ToJson()
        {
            JObject json = new();
            json["blockIndex"] = (int)BlockIndex;
            if (!string.IsNullOrEmpty(BlockHash))
                json["blockHash"] = BlockHash;
            if (!string.IsNullOrEmpty(TransactionHash))
                json["transactionHash"] = TransactionHash;
            json["limit"] = Limit;
            json["offset"] = Offset;

            json["transactionResults"] = BuildCollection(TransactionResults.Select(t => t.ToJson()), TransactionResultTotal);
            json["opcodes"] = BuildCollection(OpCodeTraces.Select(t => t.ToJson()), OpCodeTotal);
            json["syscalls"] = BuildCollection(SyscallTraces.Select(t => t.ToJson()), SyscallTotal);
            json["contractCalls"] = BuildCollection(ContractCalls.Select(t => t.ToJson()), ContractCallTotal);
            json["storageReads"] = BuildCollection(StorageReads.Select(t => t.ToJson()), StorageReadTotal);
            json["storageWrites"] = BuildCollection(StorageWrites.Select(t => t.ToJson()), StorageWriteTotal);
            json["notifications"] = BuildCollection(Notifications.Select(t => t.ToJson()), NotificationTotal);
            json["logs"] = BuildCollection(Logs.Select(t => t.ToJson()), LogTotal);
            return json;
        }

        private static JObject BuildCollection(IEnumerable<JToken> items, int total)
        {
            var array = new JArray();
            foreach (var token in items)
            {
                array.Add(token);
            }

            JObject wrapper = new();
            wrapper["total"] = total;
            wrapper["items"] = array;
            return wrapper;
        }
    }
}
