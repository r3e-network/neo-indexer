// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStateIndexerPlugin.Dispose.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.IEventHandlers;
using Neo.Ledger;
using Neo.SmartContract;
using System;

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class BlockStateIndexerPlugin
    {
        public override void Dispose()
        {
            Blockchain.Committing -= ((ICommittingHandler)this).Blockchain_Committing_Handler;
            Blockchain.Committed -= ((ICommittedHandler)this).Blockchain_Committed_Handler;

            if (_tracingProvider != null)
            {
                ApplicationEngine.Provider = _previousEngineProvider;
                _tracingProvider = null;
                _previousEngineProvider = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}

