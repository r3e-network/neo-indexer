// Copyright (C) 2015-2025 The Neo Project.
//
// SupabaseModels.BlocksContracts.cs file belongs to the neo project and is free
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
    /// <summary>
    /// DTO for blocks table in Supabase PostgreSQL.
    /// </summary>
    internal readonly record struct BlockRecord(
        int BlockIndex,
        string Hash,
        long TimestampMs,
        int TransactionCount,
        int ReadKeyCount);

    /// <summary>
    /// DTO for contracts table in Supabase PostgreSQL.
    /// </summary>
    internal readonly record struct ContractRecord(
        int ContractId,
        string ContractHash,
        string? ManifestName);
}

