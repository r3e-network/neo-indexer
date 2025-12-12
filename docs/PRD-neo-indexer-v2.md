# Product Requirements Document: Neo Indexer Turnkey Stack v2.0

## Document Information
- **Version**: 2.0
- **Status**: Draft
- **Created**: 2025-01-XX

---

## 1. Executive Summary

### 1.1 Product Vision
Transform the existing Neo Block State Indexer into a comprehensive **Neo Indexer Turnkey Stack** - a production-ready, self-hosted blockchain indexer that captures complete execution traces including OpCodes, Syscalls, contract interactions, and state changes with a debugger-style UI for analysis.

### 1.2 Problem Statement
Current Neo blockchain analysis tools lack:
- Complete VM execution trace capture (OpCode-level granularity)
- Human-readable Syscall resolution
- Contract call graph visualization
- Efficient data pruning for long-running nodes
- Real-time monitoring dashboards
- Debugger-style trace browsing UI

### 1.3 Solution Overview
Extend the existing BlockStateIndexer plugin to capture comprehensive execution data and provide a modern React-based dashboard for analysis, filtering, and visualization.

---

## 2. Goals and Success Metrics

### 2.1 Primary Goals
| Goal | Description | Priority |
|------|-------------|----------|
| G1 | Capture complete OpCode execution traces | P0 |
| G2 | Record all Syscall invocations with human-readable names | P0 |
| G3 | Track contract-to-contract interactions | P0 |
| G4 | Implement partitioned storage for efficient pruning | P1 |
| G5 | Build real-time monitoring dashboard | P1 |
| G6 | Create debugger-style trace browser UI | P1 |

### 2.2 Success Metrics
| Metric | Target | Measurement |
|--------|--------|-------------|
| Trace Completeness | 100% OpCodes captured | Audit against reference node |
| Query Latency | < 100ms for single block | P95 response time |
| Storage Efficiency | < 500MB per 10K blocks | Disk usage monitoring |
| UI Responsiveness | < 200ms interaction | Lighthouse performance |
| Data Retention | Configurable 1-90 days | Automatic pruning verification |

---

## 3. User Personas

### 3.1 Blockchain Developer
- **Needs**: Debug smart contract execution, trace transaction flow
- **Pain Points**: No visibility into VM execution, hard to diagnose failures
- **Goals**: Understand exactly what happened during contract execution

### 3.2 Security Researcher
- **Needs**: Analyze contract behavior, detect anomalies
- **Pain Points**: Manual trace reconstruction, no call graph visualization
- **Goals**: Identify suspicious patterns and vulnerabilities

### 3.3 Node Operator
- **Needs**: Monitor node health, manage storage
- **Pain Points**: Unbounded data growth, no pruning mechanism
- **Goals**: Run indexer long-term without storage issues

### 3.4 DApp Developer
- **Needs**: Query historical state, understand contract interactions
- **Pain Points**: Limited RPC capabilities, no historical queries
- **Goals**: Build applications with rich blockchain data access

---

## 4. Functional Requirements

### 4.1 Core Indexing Features

#### FR-1: OpCode Trace Capture
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-1.1 | Capture every VM instruction executed per transaction | P0 |
| FR-1.2 | Record instruction pointer, opcode, operands | P0 |
| FR-1.3 | Track evaluation stack state changes | P1 |
| FR-1.4 | Record GAS consumption per instruction | P0 |
| FR-1.5 | Support trace filtering by contract/method | P1 |

#### FR-2: Syscall Recording
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-2.1 | Intercept all Neo.* syscall invocations | P0 |
| FR-2.2 | Resolve syscall hashes to human-readable names | P0 |
| FR-2.3 | Record syscall parameters and return values | P0 |
| FR-2.4 | Track syscall GAS costs | P1 |
| FR-2.5 | Maintain syscall name mapping table | P0 |

#### FR-3: Contract Interaction Tracking
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-3.1 | Record all contract-to-contract calls | P0 |
| FR-3.2 | Track call depth and call stack | P0 |
| FR-3.3 | Capture method signatures and parameters | P1 |
| FR-3.4 | Build contract call graph per transaction | P1 |
| FR-3.5 | Track NEP-17 token transfers | P0 |

#### FR-4: State Change Recording (Existing + Enhanced)
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-4.1 | Record all storage reads (existing) | P0 |
| FR-4.2 | Record all storage writes with before/after values | P0 |
| FR-4.3 | Track storage key access patterns | P1 |
| FR-4.4 | Record notification events | P0 |

### 4.2 Data Management Features

#### FR-5: Partitioned Storage
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-5.1 | Partition data by time period (daily/weekly) | P1 |
| FR-5.2 | Support instant partition drop for pruning | P1 |
| FR-5.3 | Configurable retention period (1-90 days) | P1 |
| FR-5.4 | Automatic partition creation and rotation | P1 |
| FR-5.5 | Maintain summary statistics after pruning | P2 |

#### FR-6: Data Export
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-6.1 | Export traces in JSON format | P1 |
| FR-6.2 | Export traces in CSV format | P1 |
| FR-6.3 | Support filtered exports by block range | P1 |
| FR-6.4 | Binary format for efficient storage | P2 |

### 4.3 API Features

#### FR-7: Query API
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-7.1 | Query traces by block index | P0 |
| FR-7.2 | Query traces by transaction hash | P0 |
| FR-7.3 | Query traces by contract hash | P0 |
| FR-7.4 | Filter by opcode type | P1 |
| FR-7.5 | Filter by syscall name | P1 |
| FR-7.6 | Pagination support for large results | P0 |
| FR-7.7 | Real-time WebSocket subscriptions | P2 |

#### FR-8: RPC Extensions
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-8.1 | getblocktrace(index) - Full block trace | P0 |
| FR-8.2 | gettransactiontrace(hash) - Transaction trace | P0 |
| FR-8.3 | getcontractcalls(hash, range) - Contract interactions | P1 |
| FR-8.4 | getsyscallstats(range) - Syscall statistics | P1 |
| FR-8.5 | getopcodestats(range) - OpCode statistics | P1 |

### 4.4 Dashboard Features

#### FR-9: Real-time Monitoring
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-9.1 | Live block sync status | P0 |
| FR-9.2 | Transactions per block chart | P1 |
| FR-9.3 | GAS usage trends | P1 |
| FR-9.4 | Top contracts by activity | P1 |
| FR-9.5 | Syscall frequency distribution | P1 |

#### FR-10: Trace Browser UI
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-10.1 | Block list with search/filter | P0 |
| FR-10.2 | Transaction detail view | P0 |
| FR-10.3 | OpCode trace viewer (debugger-style) | P0 |
| FR-10.4 | Syscall timeline visualization | P1 |
| FR-10.5 | Contract call graph visualization | P1 |
| FR-10.6 | Stack state inspector | P2 |
| FR-10.7 | Storage diff viewer | P1 |

---

## 5. Non-Functional Requirements

### 5.1 Performance
| ID | Requirement | Target |
|----|-------------|--------|
| NFR-1.1 | Block processing latency | < 500ms per block |
| NFR-1.2 | API query response time | < 100ms P95 |
| NFR-1.3 | Dashboard load time | < 2s initial load |
| NFR-1.4 | Concurrent API requests | 100+ simultaneous |

### 5.2 Scalability
| ID | Requirement | Target |
|----|-------------|--------|
| NFR-2.1 | Storage per block (avg) | < 50KB compressed |
| NFR-2.2 | Blocks per partition | 100K blocks |
| NFR-2.3 | Maximum retention | 90 days configurable |

### 5.3 Reliability
| ID | Requirement | Target |
|----|-------------|--------|
| NFR-3.1 | Data consistency | 100% trace accuracy |
| NFR-3.2 | Crash recovery | Resume from last block |
| NFR-3.3 | Re-sync support | Full re-indexing capability |

### 5.4 Security
| ID | Requirement | Target |
|----|-------------|--------|
| NFR-4.1 | API authentication | API key support |
| NFR-4.2 | Rate limiting | Configurable per endpoint |
| NFR-4.3 | Input validation | All user inputs sanitized |

---

## 6. Technical Architecture

### 6.1 System Components

```
+------------------------------------------------------------------+
|                        Neo Node                                   |
|  +------------------------------------------------------------+  |
|  |                 BlockStateIndexer Plugin                    |  |
|  |  +-------------+  +-------------+  +-----------------+     |  |
|  |  | OpCode      |  | Syscall     |  | Contract        |     |  |
|  |  | Tracer      |  | Interceptor |  | Call Tracker    |     |  |
|  |  +------+------+  +------+------+  +--------+--------+     |  |
|  |         |                |                   |              |  |
|  |         +----------------+-------------------+              |  |
|  |                          v                                  |  |
|  |              +-----------------------+                      |  |
|  |              |   Trace Aggregator    |                      |  |
|  |              +-----------+-----------+                      |  |
|  +--------------------------|----------------------------------+  |
|                             |                                     |
+-----------------------------+-------------------------------------+
                              |
                              v
              +-------------------------------+
              |      Supabase Backend         |
              |  +---------+  +------------+  |
              |  |PostgreSQL|  |  Storage   |  |
              |  |(Tables)  |  |  (Binary)  |  |
              |  +---------+  +------------+  |
              +---------------+---------------+
                              |
                              v
              +-------------------------------+
              |      React Dashboard          |
              |  +---------+  +------------+  |
              |  | Monitor |  |   Trace    |  |
              |  |  Panel  |  |  Browser   |  |
              |  +---------+  +------------+  |
              +-------------------------------+
```

### 6.2 Database Schema (New Tables)

```sql
-- OpCode traces (partitioned by block range)
CREATE TABLE opcode_traces (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    trace_order INTEGER NOT NULL,
    contract_hash TEXT NOT NULL,
    instruction_pointer INTEGER NOT NULL,
    opcode SMALLINT NOT NULL,
    opcode_name TEXT NOT NULL,
    operand_base64 TEXT,
    gas_consumed BIGINT NOT NULL,
    stack_depth INTEGER,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, trace_order)
) PARTITION BY RANGE (block_index);

-- Syscall invocations
CREATE TABLE syscall_traces (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    trace_order INTEGER NOT NULL,
    contract_hash TEXT NOT NULL,
    syscall_hash TEXT NOT NULL,
    syscall_name TEXT NOT NULL,
    gas_cost BIGINT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, trace_order)
) PARTITION BY RANGE (block_index);

-- Contract calls (call graph edges)
CREATE TABLE contract_calls (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    trace_order INTEGER NOT NULL,
    caller_hash TEXT,
    callee_hash TEXT NOT NULL,
    method_name TEXT,
    call_depth INTEGER NOT NULL,
    success BOOLEAN NOT NULL DEFAULT true,
    gas_consumed BIGINT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, trace_order)
) PARTITION BY RANGE (block_index);

-- Syscall name mapping (reference table)
CREATE TABLE syscall_names (
    hash TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    category TEXT,
    description TEXT,
    gas_base BIGINT
);

-- Storage writes (extends existing storage_reads)
CREATE TABLE storage_writes (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    write_order INTEGER NOT NULL,
    contract_id INTEGER,
    contract_hash TEXT NOT NULL,
    key_base64 TEXT NOT NULL,
    old_value_base64 TEXT,
    new_value_base64 TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, write_order)
) PARTITION BY RANGE (block_index);

-- Notifications/Events
CREATE TABLE notifications (
    block_index INTEGER NOT NULL,
    tx_hash TEXT NOT NULL,
    notification_order INTEGER NOT NULL,
    contract_hash TEXT NOT NULL,
    event_name TEXT NOT NULL,
    state_json JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (block_index, tx_hash, notification_order)
) PARTITION BY RANGE (block_index);

-- Block statistics (summary, not partitioned)
CREATE TABLE block_stats (
    block_index INTEGER PRIMARY KEY,
    tx_count INTEGER NOT NULL DEFAULT 0,
    total_gas_consumed BIGINT NOT NULL DEFAULT 0,
    opcode_count INTEGER NOT NULL DEFAULT 0,
    syscall_count INTEGER NOT NULL DEFAULT 0,
    contract_call_count INTEGER NOT NULL DEFAULT 0,
    storage_read_count INTEGER NOT NULL DEFAULT 0,
    storage_write_count INTEGER NOT NULL DEFAULT 0,
    notification_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
```

### 6.3 Technology Stack

| Layer | Technology | Rationale |
|-------|------------|-----------|
| Backend | .NET 9.0 / C# | Existing Neo node stack |
| Database | PostgreSQL (Supabase) | Existing infrastructure |
| API | PostgREST + Custom RPC | REST + JSON-RPC |
| Frontend | React 18 + TypeScript | Existing dashboard |
| Visualization | D3.js / Recharts | Call graphs, charts |
| State Management | React Query | Server state caching |

---

## 7. Implementation Phases

### Phase 1: Core Tracing (2-3 weeks)
- [ ] Implement OpCode tracer in ApplicationEngine
- [ ] Implement Syscall interceptor
- [ ] Create syscall name mapping table
- [ ] Add REST API upload for new trace types
- [ ] Database schema migration

### Phase 2: Contract Tracking (1-2 weeks)
- [ ] Implement contract call tracker
- [ ] Track storage writes with diff
- [ ] Capture notification events
- [ ] Build call graph data structure

### Phase 3: Data Management (1 week)
- [ ] Implement table partitioning
- [ ] Create partition management functions
- [ ] Add configurable retention policy
- [ ] Implement automatic pruning

### Phase 4: API & RPC (1 week)
- [ ] Extend RPC server with trace endpoints
- [ ] Implement query filters
- [ ] Add pagination support
- [ ] Performance optimization

### Phase 5: Dashboard UI (2 weeks)
- [ ] Real-time monitoring panel
- [ ] Block/Transaction browser
- [ ] OpCode trace viewer (debugger-style)
- [ ] Syscall timeline
- [ ] Contract call graph visualization
- [ ] Storage diff viewer

### Phase 6: Testing & Documentation (1 week)
- [ ] Unit tests for tracers
- [ ] Integration tests
- [ ] Performance benchmarks
- [ ] User documentation
- [ ] API documentation

---

## 8. Risks and Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Performance degradation from tracing | High | Medium | Configurable trace levels, async upload |
| Storage growth explosion | High | High | Partitioning, aggressive pruning |
| ApplicationEngine API changes | Medium | Low | Abstract tracer interface |
| Supabase rate limits | Medium | Medium | Batch uploads, local buffering |

---

## 9. Open Questions

1. **Trace Granularity**: Should we capture full stack state at each instruction? (Storage vs. completeness tradeoff)
2. **Real-time vs. Batch**: Should traces be uploaded per-block or batched?
3. **Retention Defaults**: What should be the default retention period?
4. **Authentication**: Should the dashboard require authentication?

---

## 10. Appendix

### A. Syscall Categories
- **Storage**: System.Storage.*
- **Runtime**: System.Runtime.*
- **Contract**: System.Contract.*
- **Crypto**: System.Crypto.*
- **Iterator**: System.Iterator.*
- **Native**: Neo.Native.*

### B. OpCode Categories
- **Constants**: PUSH*, PUSHA, PUSHNULL
- **Flow Control**: JMP*, CALL*, RET, ABORT
- **Stack**: DUP, DROP, SWAP, ROT, ROLL
- **Arithmetic**: ADD, SUB, MUL, DIV, MOD
- **Bitwise**: AND, OR, XOR, NOT, SHL, SHR
- **Comparison**: EQUAL, NOTEQUAL, LT, GT, LE, GE
- **Compound**: PACK, UNPACK, NEWARRAY, NEWSTRUCT
- **Type**: ISNULL, ISTYPE, CONVERT

### C. Reference Implementation
- Neo VM Source: `src/Neo.VM/`
- ApplicationEngine: `src/Neo/SmartContract/ApplicationEngine.cs`
- Existing Plugin: `src/Plugins/BlockStateIndexer/`

---

## Document Approval

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Product Owner | | | |
| Tech Lead | | | |
| Architect | | | |
