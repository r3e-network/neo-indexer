// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.UploadQueue.WorkQueue.Constructor.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System.Threading.Channels;
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private sealed partial class UploadWorkQueue
        {
            public UploadWorkQueue()
            {
                _highPriorityCapacity = GetPositiveEnvIntOrDefault(UploadQueueCapacityEnvVar, DefaultHighPriorityCapacity);
                _lowPriorityCapacity = GetPositiveEnvIntOrDefault(TraceUploadQueueCapacityEnvVar, DefaultLowPriorityCapacity);

                _highPriority = CreateBoundedChannel(_highPriorityCapacity);
                _lowPriority = CreateBoundedChannel(_lowPriorityCapacity);

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

            private static Channel<UploadWorkItem> CreateBoundedChannel(int capacity)
            {
                return Channel.CreateBounded<UploadWorkItem>(new BoundedChannelOptions(capacity)
                {
                    SingleReader = false,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait
                });
            }
        }
    }
}

