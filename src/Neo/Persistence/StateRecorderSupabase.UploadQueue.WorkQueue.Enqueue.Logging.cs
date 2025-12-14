// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.UploadQueue.WorkQueue.Enqueue.Logging.cs file belongs to the neo project and is free
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
    public static partial class StateRecorderSupabase
    {
        private sealed partial class UploadWorkQueue
        {
            private void MaybeLogQueueDrop(bool isHighPriority, long dropped, string description, uint blockIndex)
            {
                if (dropped != 1 && dropped % 100 != 0)
                    return;

                Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                    $"Supabase upload queue full; dropped {(isHighPriority ? "high" : "low")} priority work. " +
                    $"pending_high={Volatile.Read(ref _pendingHighPriority)}/{_highPriorityCapacity}, " +
                    $"pending_low={Volatile.Read(ref _pendingLowPriority)}/{_lowPriorityCapacity}, " +
                    $"dropped_high={Interlocked.Read(ref _droppedHighPriority)}, dropped_low={Interlocked.Read(ref _droppedLowPriority)}. " +
                    $"Last dropped item: {description} (block {blockIndex}).");
            }
        }
    }
}

