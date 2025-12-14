// Copyright (C) 2015-2025 The Neo Project.
//
// BlockTraceRecorder.Clear.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

namespace Neo.Persistence
{
    public sealed partial class BlockTraceRecorder
    {
        /// <summary>
        /// Clears all recorded traces.
        /// </summary>
        public void Clear()
        {
            foreach (var recorder in _txRecorders.Values)
            {
                recorder.Clear();
            }
            _txRecorders.Clear();
        }
    }
}

