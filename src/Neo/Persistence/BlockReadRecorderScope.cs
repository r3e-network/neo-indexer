// Copyright (C) 2015-2025 The Neo Project.
//
// BlockReadRecorderScope.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using System;

namespace Neo.Persistence
{
    public sealed class BlockReadRecorderScope : IDisposable
    {
        private readonly BlockReadRecorder? _previous;
        private readonly UInt256? _previousTransactionHash;
        public BlockReadRecorder Recorder { get; }

        public BlockReadRecorderScope(BlockReadRecorder recorder, BlockReadRecorder? previous)
        {
            Recorder = recorder;
            _previous = previous;
            _previousTransactionHash = StateReadRecorder.TransactionHash;
            StateReadRecorder.TransactionHash = null;
            StateReadRecorder.Current = recorder;
        }

        public void Dispose()
        {
            StateReadRecorder.Current = _previous;
            StateReadRecorder.TransactionHash = _previousTransactionHash;
        }
    }
}

