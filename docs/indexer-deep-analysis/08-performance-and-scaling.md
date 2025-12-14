# 8. Performance and scaling considerations

## 8.1 Memory growth risks
- `BlockReadRecorder` holds every unique key read for a block until commit. Very “hot” blocks can be huge.
  - Use `NEO_STATE_RECORDER__MAX_STORAGE_READS_PER_BLOCK` to cap.
- `ExecutionTraceRecorder` holds per-tx traces. If trace level is `All` and blocks are large, this is substantial.
  - Use `NEO_STATE_RECORDER__TRACE_LEVEL` to reduce.
  - Use `BlockStateIndexer.json` `MinTransactionCount` to skip traces for small blocks.

## 8.2 Write amplification
- REST API mode can involve:
  - upserts for blocks/contracts
  - batched inserts/upserts for reads and traces
  - optional stale-tail deletes when trimming is enabled
  - optional per-block deletes when a tip reorg is detected (see 9.3)
- Postgres direct mode is typically lower overhead (single transaction), but requires network connectivity to Postgres.

## 8.3 Backpressure vs completeness
The bounded queue can drop work under sustained Supabase slowness:
- high priority drops mean you may miss blocks/reads/stats
- low priority drops mean you may miss traces (by design preference)

If your goal is completeness:
- raise queue capacities
- raise worker count carefully (Supabase 429 risk)
- monitor drop counters via `StateRecorderSupabase.GetUploadQueueStats()`
