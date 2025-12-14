# Neo Indexer Deep Analysis (neo-indexer)

This document is a code-accurate deep dive into how `neo-indexer` captures data from a Neo node and persists it into Supabase (Postgres + Storage), and how that data is later queried via RPC.

It complements (not replaces) the higher-level design docs:
- `docs/ARCHITECTURE-neo-indexer-v2.md`
- `docs/DEPLOYMENT-SUPABASE-MAINNET.md`
- `docs/RPC-TRACES-API.md`

The deep analysis is split into smaller files for easier navigation:

- [1. What this indexer actually does](indexer-deep-analysis/01-what-this-indexer-does.md)
- [2. End-to-end data flow (block lifecycle)](indexer-deep-analysis/02-end-to-end-data-flow.md)
- [3. Recording storage reads](indexer-deep-analysis/03-recording-storage-reads.md)
- [4. Recording execution traces](indexer-deep-analysis/04-recording-execution-traces.md)
- [5. Upload pipeline and backpressure](indexer-deep-analysis/05-upload-pipeline-and-backpressure.md)
- [6. Supabase persistence model (schema + idempotency)](indexer-deep-analysis/06-supabase-persistence-model.md)
- [7. RPC query surface (reading traces from Supabase)](indexer-deep-analysis/07-rpc-query-surface.md)
- [8. Performance and scaling considerations](indexer-deep-analysis/08-performance-and-scaling.md)
- [9. Reliability and correctness notes](indexer-deep-analysis/09-reliability-and-correctness.md)
- [10. Security considerations](indexer-deep-analysis/10-security-considerations.md)
- [11. Block state file exports (Supabase Storage)](indexer-deep-analysis/11-block-state-file-exports.md)
- [12. StateReplay plugin (replaying blocks against captured state)](indexer-deep-analysis/12-state-replay-plugin.md)
- [13. Enablement + mode matrix (plugin config vs env)](indexer-deep-analysis/13-enablement-mode-matrix.md)
