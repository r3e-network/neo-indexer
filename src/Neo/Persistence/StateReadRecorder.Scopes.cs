// Copyright (C) 2015-2025 The Neo Project.
//
// StateReadRecorder.Scopes.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Network.P2P.Payloads;
using System;

namespace Neo.Persistence
{
    public static partial class StateReadRecorder
    {
        /// <summary>
        /// Begin a transaction scope for recording reads with transaction context.
        /// </summary>
        internal static IDisposable? BeginTransaction(UInt256? txHash)
        {
            if (!Enabled) return null;
            return new TransactionScope(txHash);
        }

        /// <summary>
        /// Suppress recording temporarily (used when querying contract metadata to avoid recursion).
        /// </summary>
        internal static IDisposable SuppressRecordingScope()
        {
            RecordingSuppressed.Value = RecordingSuppressed.Value + 1;
            return new RecordingSuppressionScope();
        }

        private sealed class TransactionScope : IDisposable
        {
            private readonly UInt256? _previous;

            public TransactionScope(UInt256? txHash)
            {
                _previous = TransactionHash;
                TransactionHash = txHash;
            }

            public void Dispose()
            {
                TransactionHash = _previous;
            }
        }

        private sealed class RecordingSuppressionScope : IDisposable
        {
            public void Dispose()
            {
                var current = RecordingSuppressed.Value - 1;
                RecordingSuppressed.Value = current < 0 ? 0 : current;
            }
        }
    }
}

