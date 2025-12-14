# 1. What this indexer actually does

At a high level the system captures **two classes of data**:

1) **Storage reads (state access) during block execution**
- Recorded at the storage engine layer (so it catches reads from any subsystem that goes through the store).
- Deduplicated so only the **first read of a unique `StorageKey` per block** is recorded.
- Includes contract metadata enrichment (`contract_hash`, `manifest_name`) and optional `tx_hash` attribution.

2) **Execution traces for persisted transactions**
- Captures traces at VM/diagnostic level: opcode execution, syscalls, contract calls, storage writes, notifications.
- Stored in partitioned Postgres tables keyed by `(block_index, tx_hash, order)` for idempotent upserts.

The “indexer” is therefore a **recorder + uploader**:
- Recorder: runs inside the Neo node process and captures state/trace data while blocks are persisted.
- Uploader: batches and upserts captured data into Supabase via REST API and/or direct Postgres, plus optional Storage uploads.
