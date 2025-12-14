# 2. End-to-end data flow (block lifecycle)

```
Neo node
  ├─ Storage reads happen (DB lookups)
  │    └─ RecordingStoreProvider wrapper calls StateReadRecorder.Record(...)
  │         └─ BlockReadRecorder dedupes + stores the first read of each key
  │
  ├─ Transactions execute during block persist
  │    └─ TracingApplicationEngineProvider creates TracingApplicationEngine
  │         └─ TracingDiagnostic/TracingEngine record opcodes/syscalls/calls/writes/notifications/runtime logs
  │             └─ ExecutionTraceRecorder (per tx)
  │
  └─ Block committed event fires
       └─ BlockStateIndexerPlugin drains recorders for this block
            ├─ StateRecorderSupabase.TryUpload(BlockReadRecorder, mode)
            ├─ StateRecorderSupabase.TryQueueTraceUpload(blockIndex, blockHash, ExecutionTraceRecorder) [per tx]
            └─ StateRecorderSupabase.TryQueueBlockStatsUpload(BlockStats, blockHash)
                 └─ Background upload queue + concurrency throttles + retries
```
