// Copyright (C) 2015-2025 The Neo Project.
//
// ExecutionTraceRecorder.Stats.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Threading;

namespace Neo.Persistence
{
    public sealed partial class ExecutionTraceRecorder
    {
        /// <summary>
        /// Gets aggregated statistics for this transaction.
        /// </summary>
        public BlockStats GetStats()
        {
            var resolvedTotalGasConsumed = TotalGasConsumed ?? ResolveTotalGasFromOpCodes();

            return new BlockStats
            {
                BlockIndex = BlockIndex,
                TransactionCount = 1,
                TotalGasConsumed = resolvedTotalGasConsumed,
                OpCodeCount = Volatile.Read(ref _opCodeCount),
                SyscallCount = Volatile.Read(ref _syscallCount),
                ContractCallCount = Volatile.Read(ref _contractCallCount),
                StorageReadCount = 0, // Filled by BlockReadRecorder
                StorageWriteCount = Volatile.Read(ref _storageWriteCount),
                NotificationCount = Volatile.Read(ref _notificationCount)
            };
        }

        private long ResolveTotalGasFromOpCodes()
        {
            // Fallback path: TotalGasConsumed is normally filled by TracingDiagnostic.Disposed.
            // Avoid materializing/sorting a list here; just scan the recorded opcodes.
            long sum = 0;
            bool hasAny = false;
            bool looksCumulative = true;
            long previous = 0;
            long last = 0;

            foreach (var trace in _opCodeTraces)
            {
                var gas = trace.GasConsumed;
                if (!hasAny)
                {
                    hasAny = true;
                }
                else if (gas < previous)
                {
                    looksCumulative = false;
                }

                previous = gas;
                last = gas;
                sum += gas;
            }

            if (!hasAny) return 0;
            return looksCumulative ? last : sum;
        }

        /// <summary>
        /// Clears all recorded traces.
        /// </summary>
        public void Clear()
        {
            while (_opCodeTraces.TryDequeue(out _)) { }
            while (_syscallTraces.TryDequeue(out _)) { }
            while (_contractCalls.TryDequeue(out _)) { }
            while (_storageWrites.TryDequeue(out _)) { }
            while (_notifications.TryDequeue(out _)) { }

            _opCodeOrder = 0;
            _syscallOrder = 0;
            _contractCallOrder = 0;
            _storageWriteOrder = 0;
            _notificationOrder = 0;
            _opCodeCount = 0;
            _syscallCount = 0;
            _contractCallCount = 0;
            _storageWriteCount = 0;
            _notificationCount = 0;
        }
    }
}

