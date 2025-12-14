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

using System.Threading.Channels;

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
        }
    }
}
