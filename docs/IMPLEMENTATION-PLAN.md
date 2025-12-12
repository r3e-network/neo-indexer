# Implementation Plan: Neo Indexer v2.0

## Overview

This document provides a detailed implementation plan with specific tasks, file changes, and dependencies.

---

## Sprint 1: Core Tracing Infrastructure (Week 1-2)

### Task 1.1: Data Models

**Priority**: P0 | **Effort**: 2h

Create trace data models in `src/Neo/Persistence/`:

```
New Files:
- src/Neo/Persistence/TraceModels.cs
  - OpCodeTrace record
  - SyscallTrace record
  - ContractCall record
  - StorageWrite record
  - NotificationEvent record
```

### Task 1.2: ExecutionTraceRecorder

**Priority**: P0 | **Effort**: 4h | **Depends**: 1.1

```
New File:
- src/Neo/Persistence/ExecutionTraceRecorder.cs
  - Thread-safe trace collection
  - RecordOpCode(), RecordSyscall(), etc.
  - GetTraces() for upload
```

### Task 1.3: TracingDiagnostic

**Priority**: P0 | **Effort**: 4h | **Depends**: 1.2

```
New File:
- src/Neo/SmartContract/TracingDiagnostic.cs
  - Implements IDiagnostic
  - PreExecuteInstruction -> RecordOpCode
  - ContextLoaded -> RecordContractCall
```

### Task 1.4: TracingApplicationEngine

**Priority**: P0 | **Effort**: 6h | **Depends**: 1.2

```
New File:
- src/Neo/SmartContract/TracingApplicationEngine.cs
  - Extends ApplicationEngine
  - Override OnSysCall for syscall capture
  - Integrate with ExecutionTraceRecorder
```

### Task 1.5: TracingApplicationEngineProvider

**Priority**: P0 | **Effort**: 2h | **Depends**: 1.3, 1.4

```
New File:
- src/Neo/SmartContract/TracingApplicationEngineProvider.cs
  - Implements IApplicationEngineProvider
  - Creates TracingApplicationEngine with TracingDiagnostic
```

### Task 1.6: Plugin Integration

**Priority**: P0 | **Effort**: 4h | **Depends**: 1.5

```
Modified Files:
- src/Plugins/BlockStateIndexer/BlockStateIndexer.cs
  - Register TracingApplicationEngineProvider on startup
  - Collect traces after block commit
  - Trigger upload via StateRecorderSupabase
```

---

## Sprint 2: Database Schema & Upload (Week 2-3)

### Task 2.1: SQL Migration - New Tables

**Priority**: P0 | **Effort**: 2h

```
New File:
- migrations/002_trace_tables.sql
  - opcode_traces (partitioned)
  - syscall_traces (partitioned)
  - contract_calls (partitioned)
  - storage_writes (partitioned)
  - notifications (partitioned)
  - block_stats
  - syscall_names (reference)
```

### Task 2.2: Syscall Name Seed Data

**Priority**: P1 | **Effort**: 2h

```
New File:
- migrations/003_syscall_names.sql
  - Seed all Neo syscall names from ApplicationEngine.Services
  - Categories: Storage, Runtime, Contract, Crypto, etc.
```

### Task 2.3: Partition Management Functions

**Priority**: P1 | **Effort**: 4h | **Depends**: 2.1

```
Included in:
- migrations/002_trace_tables.sql
  - create_trace_partition(table_name, start_block, end_block)
  - prune_old_partitions(table_name, retention_blocks)
  - get_partition_stats()
```

### Task 2.4: StateRecorderSupabase Extensions

**Priority**: P0 | **Effort**: 8h | **Depends**: 2.1

```
Modified File:
- src/Neo/Persistence/StateRecorderSupabase.cs
  - Add UploadOpCodeTracesRestApiAsync()
  - Add UploadSyscallTracesRestApiAsync()
  - Add UploadContractCallsRestApiAsync()
  - Add UploadNotificationsRestApiAsync()
  - Add UploadBlockStatsRestApiAsync()
  - Batch upload with configurable size
```

### Task 2.5: Extended Settings

**Priority**: P1 | **Effort**: 2h

```
Modified File:
- src/Neo/Persistence/StateReadRecorder.cs
  - Add TraceLevel flag settings
  - Add UploadMode / RestApi configuration
  - Add TRACE_BATCH_SIZE env var
  - Environment variable support
```

---

## Sprint 3: RPC Extensions (Week 3-4)

### Task 3.1: RPC Server Trace Methods

**Priority**: P0 | **Effort**: 6h | **Depends**: 2.4

```
New File:
- src/Plugins/RpcServer/RpcServer.Traces.cs
  - GetBlockTrace(blockIndex)
  - GetTransactionTrace(txHash)
  - GetContractCalls(contractHash, startBlock, endBlock)
  - GetSyscallStats(startBlock, endBlock)
  - GetOpCodeStats(startBlock, endBlock)
```

### Task 3.2: Query Filters & Pagination

**Priority**: P1 | **Effort**: 4h | **Depends**: 3.1

```
Modified File:
- src/Plugins/RpcServer/RpcServer.Traces.cs
  - Add filter parameters (opcode, syscall, contract)
  - Add pagination (limit, offset)
  - Add sorting options
```

### Task 3.3: RPC Documentation

**Priority**: P2 | **Effort**: 2h | **Depends**: 3.1

```
New File:
- docs/RPC-TRACES-API.md
  - Method signatures
  - Request/Response examples
  - Error codes
```

---

## Sprint 4: Frontend Dashboard (Week 4-5)

### Task 4.1: API Client Extensions

**Priority**: P0 | **Effort**: 4h | **Depends**: 3.1

```
Modified File:
- frontend/src/services/api.ts
  - fetchBlockTrace(blockIndex)
  - fetchTransactionTrace(txHash)
  - fetchContractCalls(contractHash, range)
  - fetchSyscallStats(range)
  - fetchOpCodeStats(range)
```

### Task 4.2: Trace Browser Page

**Priority**: P0 | **Effort**: 8h | **Depends**: 4.1

```
New Files:
- frontend/src/pages/TraceBrowser.tsx
  - Block/Transaction selector
  - Tab navigation (OpCodes, Syscalls, Calls, Storage)
- frontend/src/components/traces/
  - OpCodeViewer.tsx (debugger-style table)
  - SyscallTimeline.tsx (timeline visualization)
  - CallGraph.tsx (D3.js graph)
  - StorageDiff.tsx (before/after diff)
```

### Task 4.3: OpCode Viewer Component

**Priority**: P0 | **Effort**: 6h | **Depends**: 4.1

```
New File:
- frontend/src/components/traces/OpCodeViewer.tsx
  - Monospace table with syntax highlighting
  - OpCode category coloring
  - Operand formatting (hex, base64, string)
  - GAS consumption column
  - Stack depth indicator
  - Search/filter by opcode
```

### Task 4.4: Syscall Timeline Component

**Priority**: P1 | **Effort**: 4h | **Depends**: 4.1

```
New File:
- frontend/src/components/traces/SyscallTimeline.tsx
  - Horizontal timeline visualization
  - Syscall category grouping
  - GAS cost indicators
  - Click to expand details
```

### Task 4.5: Contract Call Graph Component

**Priority**: P1 | **Effort**: 6h | **Depends**: 4.1

```
New File:
- frontend/src/components/traces/CallGraph.tsx
  - D3.js force-directed graph
  - Nodes = contracts
  - Edges = calls with method names
  - Call depth visualization
  - Click to filter traces
```

### Task 4.6: Real-time Stats Dashboard

**Priority**: P1 | **Effort**: 4h | **Depends**: 4.1

```
Modified File:
- frontend/src/pages/Dashboard.tsx
  - Add OpCode frequency chart
  - Add Syscall distribution pie chart
  - Add GAS usage trends
  - Add top contracts by activity
```

### Task 4.7: Navigation & Routing

**Priority**: P0 | **Effort**: 2h | **Depends**: 4.2

```
Modified Files:
- frontend/src/App.tsx
  - Add /traces route
  - Add /traces/:blockIndex route
  - Add /traces/tx/:txHash route
- frontend/src/components/Navbar.tsx
  - Add Traces navigation link
```

---

## Sprint 5: Testing & Documentation (Week 5-6)

### Task 5.1: Unit Tests - Tracing

**Priority**: P0 | **Effort**: 6h

```
New Files:
- tests/Neo.UnitTests/SmartContract/UT_TracingDiagnostic.cs
- tests/Neo.UnitTests/SmartContract/UT_TracingApplicationEngine.cs
- tests/Neo.UnitTests/Persistence/UT_ExecutionTraceRecorder.cs
```

### Task 5.2: Integration Tests - Upload

**Priority**: P1 | **Effort**: 4h

```
New File:
- tests/Neo.Plugins.BlockStateIndexer.Tests/UT_TraceUpload.cs
  - Test REST API upload for all trace types
  - Test batch upload
  - Test error handling
```

### Task 5.3: Frontend Tests

**Priority**: P1 | **Effort**: 4h

```
New Files:
- frontend/src/test/TraceBrowser.test.tsx
- frontend/src/test/OpCodeViewer.test.tsx
- frontend/src/test/CallGraph.test.tsx
```

### Task 5.4: Performance Benchmarks

**Priority**: P2 | **Effort**: 4h

```
New File:
- benchmarks/TracingBenchmarks.cs
  - Measure tracing overhead per block
  - Measure upload latency
  - Memory usage profiling
```

### Task 5.5: User Documentation

**Priority**: P1 | **Effort**: 4h

```
New/Modified Files:
- README.md (update with new features)
- docs/USER-GUIDE.md
  - Configuration options
  - Dashboard usage
  - RPC API examples
```

---

## File Summary

### New Files (Backend)

| File                                                        | Description                |
| ----------------------------------------------------------- | -------------------------- |
| `src/Neo/Persistence/TraceModels.cs`                        | Data models for traces     |
| `src/Neo/Persistence/ExecutionTraceRecorder.cs`             | Trace aggregator           |
| `src/Neo/SmartContract/TracingDiagnostic.cs`                | IDiagnostic implementation |
| `src/Neo/SmartContract/TracingApplicationEngine.cs`         | Custom ApplicationEngine   |
| `src/Neo/SmartContract/TracingApplicationEngineProvider.cs` | Engine provider            |
| `src/Plugins/RpcServer/RpcServer.Traces.cs`                 | RPC trace methods          |
| `migrations/002_trace_tables.sql`                           | New database tables        |
| `migrations/003_syscall_names.sql`                          | Syscall reference data     |

### Modified Files (Backend)

| File                                                 | Changes                   |
| ---------------------------------------------------- | ------------------------- |
| `src/Neo/Persistence/StateRecorderSupabase.cs`       | Add trace upload methods  |
| `src/Neo/Persistence/StateReadRecorder.cs`           | Add trace settings        |
| `src/Plugins/BlockStateIndexer/BlockStateIndexer.cs` | Register tracing provider |

### New Files (Frontend)

| File                                                 | Description             |
| ---------------------------------------------------- | ----------------------- |
| `frontend/src/pages/TraceBrowser.tsx`                | Main trace browser page |
| `frontend/src/components/traces/OpCodeViewer.tsx`    | OpCode table            |
| `frontend/src/components/traces/SyscallTimeline.tsx` | Syscall timeline        |
| `frontend/src/components/traces/CallGraph.tsx`       | Contract call graph     |
| `frontend/src/components/traces/StorageDiff.tsx`     | Storage diff viewer     |

### Modified Files (Frontend)

| File                               | Changes               |
| ---------------------------------- | --------------------- |
| `frontend/src/services/api.ts`     | Add trace API methods |
| `frontend/src/App.tsx`             | Add trace routes      |
| `frontend/src/pages/Dashboard.tsx` | Add stats charts      |

---

## Dependency Graph

```
1.1 TraceModels
    |
    v
1.2 ExecutionTraceRecorder
    |
    +---> 1.3 TracingDiagnostic
    |           |
    +---> 1.4 TracingApplicationEngine
                |
                v
            1.5 TracingApplicationEngineProvider
                |
                v
            1.6 Plugin Integration
                |
    +-----------+-----------+
    |                       |
    v                       v
2.1 SQL Migration       2.5 Extended Settings
    |
    v
2.4 StateRecorderSupabase Extensions
    |
    v
3.1 RPC Server Trace Methods
    |
    v
4.1 API Client Extensions
    |
    +---> 4.2 TraceBrowser Page
    |           |
    +---> 4.3 OpCodeViewer
    |
    +---> 4.4 SyscallTimeline
    |
    +---> 4.5 CallGraph
    |
    +---> 4.6 Stats Dashboard
```

---

## Risk Mitigation

| Risk                       | Mitigation                              | Owner |
| -------------------------- | --------------------------------------- | ----- |
| Tracing performance impact | Configurable trace levels, async upload | Dev   |
| Memory pressure            | Object pooling, batch processing        | Dev   |
| Storage growth             | Partitioning, automatic pruning         | DBA   |
| API breaking changes       | Separate /traces namespace              | Dev   |

---

## Definition of Done

- [ ] All unit tests pass
- [ ] Integration tests pass
- [ ] Frontend tests pass
- [ ] Code review completed
- [ ] Documentation updated
- [ ] Performance benchmarks acceptable
- [ ] No regressions in existing functionality
