// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.UploadQueue.WorkQueue.Worker.cs file belongs to the neo project and is free
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
using System.Threading.Tasks;

namespace Neo.Persistence
{
    public static partial class StateRecorderSupabase
    {
        private sealed partial class UploadWorkQueue
        {
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
        }
    }
}

