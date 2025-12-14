# 13. Enablement + mode matrix (plugin config vs env)

There are two layers of configuration that jointly determine whether data is captured and where it goes:

1) **Plugin config** (`src/Plugins/BlockStateIndexer/BlockStateIndexer.json`)
- `Enabled`: gates whether `Blockchain.Committed` handling is active
- `UploadMode`: what the plugin *intends* to upload (Binary / RestApi / Both / Postgres)
- `MinTransactionCount`: trace upload gate (see below)

2) **Environment variables** (`StateRecorderSettings`, prefix `NEO_STATE_RECORDER__`)
- `ENABLED`: gates whether recorders are activated
- `SUPABASE_URL` + `SUPABASE_KEY`: enable REST/Storage uploads (`UploadEnabled`)
- `SUPABASE_CONNECTION_STRING`: enables direct Postgres uploads
- `UPLOAD_MODE`: limits which upload modes are allowed at runtime
- plus operational knobs like `TRACE_LEVEL`, `TRACE_UPLOAD_CONCURRENCY`, `UPLOAD_QUEUE_*`, `MAX_STORAGE_READS_PER_BLOCK`, etc.

## 13.1 What must be true to capture traces/reads

`BlockStateIndexer` registers the tracing provider only when BOTH are enabled:
- `Settings.Default.Enabled` (plugin JSON)
- `StateRecorderSettings.Current.Enabled` (env)

See:
- `src/Plugins/BlockStateIndexer/BlockStateIndexerPlugin.OnSystemLoaded.cs`

## 13.2 Upload allow-listing (Binary vs DB)

On each committed block, the plugin computes:
- `allowBinaryUploads = (plugin allows Binary) AND (env allows Binary)`
- `allowRestApiUploads = (plugin allows RestApi/Postgres) AND (env allows RestApi/Postgres)`

See:
- `src/Plugins/BlockStateIndexer/BlockStateIndexerPlugin.Handlers.Committed.UploadModes.cs`

## 13.3 `MinTransactionCount` behavior

`MinTransactionCount` applies to **trace uploads** (not to read recording):
- If `block.Transactions.Length < MinTransactionCount`, traces are skipped (reads may still be uploaded).

See:
- `src/Plugins/BlockStateIndexer/BlockStateIndexerPlugin.Handlers.Committed.Traces.cs`

## 13.4 REST API vs Postgres fallback behavior

Within `StateRecorderSupabase`, “database upload” chooses REST vs direct Postgres based on the effective mode and available credentials:
- `RestApi/Both`: prefers REST when URL/key exist, otherwise falls back to Postgres when a connection string exists.
- `Postgres`: prefers Postgres when a connection string exists, otherwise falls back to REST when URL/key exist.

See:
- `src/Neo/Persistence/StateRecorderSupabase.Dispatch.Database.cs`
