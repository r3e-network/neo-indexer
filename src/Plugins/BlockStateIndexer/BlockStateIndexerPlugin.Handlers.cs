// Copyright (C) 2015-2025 The Neo Project.
//
// BlockStateIndexerPlugin.Handlers.cs file belongs to the neo project and is free
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
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System.Collections.Generic;

namespace Neo.Plugins.BlockStateIndexer
{
    public sealed partial class BlockStateIndexerPlugin
    {
        /// <summary>
        /// Called when a block is about to be committed.
        /// Starts recording state reads if the block meets criteria.
        /// </summary>
        void ICommittingHandler.Blockchain_Committing_Handler(
            global::Neo.NeoSystem system,
            Block block,
            DataCache snapshot,
            IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            // Read/trace recorders are started via TracingApplicationEngineProvider on OnPersist.
            return;
        }

        /// <summary>
        /// Called after a block has been committed.
        /// Triggers upload of recorded state reads.
        /// </summary>
        void ICommittedHandler.Blockchain_Committed_Handler(global::Neo.NeoSystem system, Block block)
        {
            HandleCommitted(system, block);
        }
    }
}
