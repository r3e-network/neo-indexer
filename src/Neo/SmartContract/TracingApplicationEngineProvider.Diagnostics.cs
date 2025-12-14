// Copyright (C) 2015-2025 The Neo Project.
//
// TracingApplicationEngineProvider.Diagnostics.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

#nullable enable

using Neo.Persistence;

namespace Neo.SmartContract
{
    public sealed partial class TracingApplicationEngineProvider
    {
        private (ExecutionTraceRecorder Recorder, IDiagnostic Diagnostic) ResolveDiagnosticAndRecorder(
            IDiagnostic? requested,
            ExecutionTraceRecorder recorder)
        {
            if (requested is TracingDiagnostic tracingDiagnostic)
            {
                ConfigureDiagnostic(tracingDiagnostic);
                return (tracingDiagnostic.Recorder, tracingDiagnostic);
            }

            var tracing = new TracingDiagnostic(recorder);
            var readTxDiagnostic = new StateReadTransactionDiagnostic();
            ConfigureDiagnostic(tracing);

            if (requested is null)
                return (recorder, new DiagnosticCollection(tracing, readTxDiagnostic));

            return (recorder, new DiagnosticCollection(tracing, readTxDiagnostic, requested));
        }

        private void ConfigureDiagnostic(TracingDiagnostic diagnostic)
        {
            diagnostic.TraceOpCodes = (_traceLevel & ExecutionTraceLevel.OpCodes) != 0;
            diagnostic.TraceContractCalls = (_traceLevel & ExecutionTraceLevel.ContractCalls) != 0;
        }
    }
}
