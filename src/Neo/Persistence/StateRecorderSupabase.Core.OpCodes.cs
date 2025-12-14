// Copyright (C) 2015-2025 The Neo Project.
//
// StateRecorderSupabase.Core.OpCodes.cs file belongs to the neo project and is free
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
    public static partial class StateRecorderSupabase
    {
        private static string[] BuildOpCodeNameCache()
        {
            var names = new string[256];
            foreach (var opCode in (VM.OpCode[])Enum.GetValues(typeof(VM.OpCode)))
            {
                names[(byte)opCode] = opCode.ToString();
            }
            return names;
        }

        private static string GetOpCodeName(VM.OpCode opCode)
        {
            return OpCodeNameCache[(byte)opCode] ?? opCode.ToString();
        }
    }
}

