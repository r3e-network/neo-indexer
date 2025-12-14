// Copyright (C) 2015-2025 The Neo Project.
//
// TracingApplicationEngineProvider.Diagnostics.Collection.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.VM;
using System;
using System.Collections.Generic;

namespace Neo.SmartContract
{
    public sealed partial class TracingApplicationEngineProvider
    {
        private sealed class DiagnosticCollection : IDiagnostic
        {
            private readonly IReadOnlyList<IDiagnostic> _diagnostics;

            public DiagnosticCollection(params IDiagnostic[] diagnostics)
            {
                _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            }

            public void Initialized(ApplicationEngine engine)
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.Initialized(engine);
            }

            public void Disposed()
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.Disposed();
            }

            public void ContextLoaded(ExecutionContext context)
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.ContextLoaded(context);
            }

            public void ContextUnloaded(ExecutionContext context)
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.ContextUnloaded(context);
            }

            public void PreExecuteInstruction(Instruction instruction)
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.PreExecuteInstruction(instruction);
            }

            public void PostExecuteInstruction(Instruction instruction)
            {
                foreach (var diagnostic in _diagnostics)
                    diagnostic?.PostExecuteInstruction(instruction);
            }
        }
    }
}

