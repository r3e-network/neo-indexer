// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.UploadQueue.WorkQueue.cs file belongs to the neo project and is free
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
            private const int DefaultHighPriorityCapacity = 2048;
            private const int DefaultLowPriorityCapacity = 16384;

            private readonly Channel<UploadWorkItem> _highPriority;
            private readonly Channel<UploadWorkItem> _lowPriority;
            private readonly int _highPriorityCapacity;
            private readonly int _lowPriorityCapacity;

            private int _pendingHighPriority;
            private int _pendingLowPriority;
            private long _droppedHighPriority;
            private long _droppedLowPriority;

            public UploadWorkQueue()
            {
                _highPriorityCapacity = GetPositiveEnvIntOrDefault(UploadQueueCapacityEnvVar, DefaultHighPriorityCapacity);
                _lowPriorityCapacity = GetPositiveEnvIntOrDefault(TraceUploadQueueCapacityEnvVar, DefaultLowPriorityCapacity);

                _highPriority = Channel.CreateBounded<UploadWorkItem>(new BoundedChannelOptions(_highPriorityCapacity)
                {
                    SingleReader = false,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                });

                _lowPriority = Channel.CreateBounded<UploadWorkItem>(new BoundedChannelOptions(_lowPriorityCapacity)
                {
                    SingleReader = false,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                });

                var workers = GetUploadQueueWorkers();
                if (workers <= 1)
                {
                    _ = Task.Run(() => WorkerLoopAsync(allowLowPriority: true));
                }
                else
                {
                    // Ensure high-priority uploads always have at least one dedicated worker,
                    // even when low-priority trace uploads are slow.
                    _ = Task.Run(() => WorkerLoopAsync(allowLowPriority: false));
                    for (var i = 1; i < workers; i++)
                    {
                        _ = Task.Run(() => WorkerLoopAsync(allowLowPriority: true));
                    }
                }
            }

            public UploadQueueStats GetStats()
            {
                return new UploadQueueStats(
                    PendingHighPriority: Volatile.Read(ref _pendingHighPriority),
                    PendingLowPriority: Volatile.Read(ref _pendingLowPriority),
                    DroppedHighPriority: Interlocked.Read(ref _droppedHighPriority),
                    DroppedLowPriority: Interlocked.Read(ref _droppedLowPriority));
            }

            private readonly record struct UploadWorkItem(
                uint BlockIndex,
                string Description,
                Func<Task> Work,
                bool IsHighPriority);
        }
    }
}
