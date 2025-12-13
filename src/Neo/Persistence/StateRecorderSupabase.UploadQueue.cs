// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.UploadQueue.cs file belongs to the neo project and is free
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
        public readonly record struct UploadQueueStats(
            int PendingHighPriority,
            int PendingLowPriority,
            long DroppedHighPriority,
            long DroppedLowPriority)
        {
            public int TotalPending => PendingHighPriority + PendingLowPriority;
        }

        public static UploadQueueStats GetUploadQueueStats() => UploadQueue.GetStats();

        private sealed class UploadWorkQueue
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

                if (dropped == 1 || dropped % 100 == 0)
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                        $"Supabase upload queue full; dropped {(isHighPriority ? "high" : "low")} priority work. " +
                        $"pending_high={Volatile.Read(ref _pendingHighPriority)}/{_highPriorityCapacity}, " +
                        $"pending_low={Volatile.Read(ref _pendingLowPriority)}/{_lowPriorityCapacity}, " +
                        $"dropped_high={Interlocked.Read(ref _droppedHighPriority)}, dropped_low={Interlocked.Read(ref _droppedLowPriority)}. " +
                        $"Last dropped item: {description} (block {blockIndex}).");
                }

                return false;
            }

            private async Task WorkerLoopAsync(bool allowLowPriority)
            {
                while (true)
                {
                    if (_highPriority.Reader.TryRead(out var item) || (allowLowPriority && _lowPriority.Reader.TryRead(out item)))
                    {
                        await ProcessAsync(item).ConfigureAwait(false);
                        continue;
                    }

                    if (!allowLowPriority)
                    {
                        await _highPriority.Reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false);
                        continue;
                    }

                    var highWait = _highPriority.Reader.WaitToReadAsync(CancellationToken.None).AsTask();
                    var lowWait = _lowPriority.Reader.WaitToReadAsync(CancellationToken.None).AsTask();
                    await Task.WhenAny(highWait, lowWait).ConfigureAwait(false);
                }
            }

            private async Task ProcessAsync(UploadWorkItem item)
            {
                try
                {
                    await item.Work().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Utility.Log(nameof(StateRecorderSupabase), LogLevel.Warning,
                        $"Supabase queued {item.Description} failed for block {item.BlockIndex}: {ex.Message}");
                }
                finally
                {
                    if (item.IsHighPriority)
                        Interlocked.Decrement(ref _pendingHighPriority);
                    else
                        Interlocked.Decrement(ref _pendingLowPriority);
                }
            }

            private readonly record struct UploadWorkItem(
                uint BlockIndex,
                string Description,
                Func<Task> Work,
                bool IsHighPriority);
        }
    }
}

