// Copyright (C) 2015-2025 The Neo Project.
//
// StateReadRecorder.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System.Threading;

namespace Neo.Persistence
{
    public static partial class StateReadRecorder
    {
        private static readonly AsyncLocal<BlockReadRecorder?> CurrentRecorder = new();
        private static readonly AsyncLocal<UInt256?> CurrentTransaction = new();
        private static readonly AsyncLocal<string?> CurrentSource = new();
        private static readonly AsyncLocal<int> RecordingSuppressed = new();

        internal static BlockReadRecorder? Current
        {
            get => CurrentRecorder.Value;
            set => CurrentRecorder.Value = value;
        }

        internal static UInt256? TransactionHash
        {
            get => CurrentTransaction.Value;
            set => CurrentTransaction.Value = value;
        }

        internal static string? Source
        {
            get => CurrentSource.Value;
            set => CurrentSource.Value = value;
        }

        private static bool IsRecordingSuppressed => RecordingSuppressed.Value > 0;

        public static bool Enabled => Current != null || StateRecorderSettings.Current.Enabled;

        /// <summary>
        /// True when a recorder scope is active on this async context and recording isn't suppressed.
        /// Useful for avoiding unnecessary work (e.g., decoding keys/values) when no recorder is present.
        /// </summary>
        public static bool IsRecording => !IsRecordingSuppressed && Current is { IsFull: false };

        public static BlockReadRecorderScope? TryBegin(Block block)
        {
            if (!Enabled) return null;
            var maxEntries = StateRecorderSettings.Current.MaxStorageReadsPerBlock;
            var recorder = new BlockReadRecorder(block, maxEntries);
            return new BlockReadRecorderScope(recorder, Current);
        }

        /// <summary>
        /// Record a storage read. Only the first read of each key per block is recorded.
        /// </summary>
        public static void Record(IReadOnlyStore? store, StorageKey key, StorageItem value, string source, UInt256? txHash = null)
        {
            if (IsRecordingSuppressed) return;
            var recorder = Current;
            if (recorder is null || recorder.IsFull) return;

            txHash ??= TransactionHash;
            var effectiveSource = Source ?? source;
            recorder.TryAdd(store, key, value, effectiveSource, txHash);
        }
    }
}
