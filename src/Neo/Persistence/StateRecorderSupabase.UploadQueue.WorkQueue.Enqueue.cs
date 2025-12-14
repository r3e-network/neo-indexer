// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.UploadQueue.WorkQueue.Enqueue.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private sealed partial class UploadWorkQueue
        {
            public bool TryEnqueueHigh(uint blockIndex, string description, Func<Task> work)
            {
                return TryEnqueue(
                    _highPriority.Writer,
                    blockIndex,
                    description,
                    work,
                    isHighPriority: true);
            }

            public bool TryEnqueueLow(uint blockIndex, string description, Func<Task> work)
            {
                return TryEnqueue(
                    _lowPriority.Writer,
                    blockIndex,
                    description,
                    work,
                    isHighPriority: false);
            }

            private bool TryEnqueue(
                ChannelWriter<UploadWorkItem> writer,
                uint blockIndex,
                string description,
                Func<Task> work,
                bool isHighPriority)
            {
                if (work is null) throw new ArgumentNullException(nameof(work));

                if (isHighPriority)
                    Interlocked.Increment(ref _pendingHighPriority);
                else
                    Interlocked.Increment(ref _pendingLowPriority);

                var item = new UploadWorkItem(blockIndex, description, work, isHighPriority);
                if (writer.TryWrite(item))
                    return true;

                if (isHighPriority)
                    Interlocked.Decrement(ref _pendingHighPriority);
                else
                    Interlocked.Decrement(ref _pendingLowPriority);

                var dropped = isHighPriority
                    ? Interlocked.Increment(ref _droppedHighPriority)
                    : Interlocked.Increment(ref _droppedLowPriority);
                MaybeLogQueueDrop(isHighPriority, dropped, description, blockIndex);

                return false;
            }
        }
    }
}
