// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.RecordBuilders.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Extensions;
using Neo.IO;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
#if NET9_0_OR_GREATER
using Npgsql;
using NpgsqlTypes;
#endif


namespace Neo.Persistence
{
	public static partial class StateRecorderSupabase
	{
        #region Record Builders

        private static BlockRecord BuildBlockRecord(BlockReadRecorder recorder, BlockReadEntry[] entries)
        {
            var timestamp = recorder.Timestamp <= long.MaxValue ? (long)recorder.Timestamp : long.MaxValue;
            return new BlockRecord(
                checked((int)recorder.BlockIndex),
                recorder.BlockHash.ToString(),
                timestamp,
                recorder.TransactionCount,
                entries.Length);
        }

	        private static List<StorageReadRecord> BuildStorageReadRecords(BlockReadRecorder recorder, BlockReadEntry[] entries)
	        {
	            var blockIndex = checked((int)recorder.BlockIndex);
	            var reads = new List<StorageReadRecord>(entries.Length);
	            foreach (var entry in entries)
	            {
	                var contractId = entry.Key.Id;
	                var keyBase64 = Convert.ToBase64String(entry.Key.Key.Span);
	                var valueBase64 = Convert.ToBase64String(entry.Value.Value.Span);
	                reads.Add(new StorageReadRecord(
	                    blockIndex,
	                    contractId,
	                    keyBase64,
	                    valueBase64,
	                    entry.Order,
	                    entry.TxHash?.ToString(),
	                    entry.Source));
	            }
	            return reads;
	        }

        private static List<ContractRecord> BuildContractRecords(BlockReadEntry[] entries)
        {
            var records = new List<ContractRecord>();
            var seen = new HashSet<int>();
            foreach (var entry in entries)
            {
                var contractId = entry.Key.Id;
                if (!seen.Add(contractId)) continue; // Skip duplicates in this block
                if (ContractCache.ContainsKey(contractId)) continue; // Skip already cached

                records.Add(new ContractRecord(contractId, entry.ContractHash.ToString(), entry.ManifestName));
            }
            return records;
        }

        private static BlockReadEntry[] GetOrderedEntries(BlockReadRecorder recorder)
        {
            if (recorder is null) throw new ArgumentNullException(nameof(recorder));

            var entries = recorder.Entries;
            if (entries.Count == 0)
                return Array.Empty<BlockReadEntry>();

            var snapshot = new BlockReadEntry[entries.Count];
            var index = 0;
            foreach (var entry in entries)
                snapshot[index++] = entry;

            for (var i = 1; i < snapshot.Length; i++)
            {
                if (snapshot[i].Order < snapshot[i - 1].Order)
                {
                    Array.Sort(snapshot, static (a, b) => a.Order.CompareTo(b.Order));
                    break;
                }
            }
            return snapshot;
        }

        #endregion
	}
}
