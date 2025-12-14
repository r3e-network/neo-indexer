# 11. Block state file exports (Supabase Storage)

In addition to writing rows into Postgres (via REST API or direct Postgres), the recorder can upload **per-block snapshot files** to Supabase Storage.

Where this is implemented:
- `src/Neo/Persistence/StateRecorderSupabase.Dispatch.Binary.cs`
- `src/Neo/Persistence/StateRecorderSupabase.BinaryUpload.*.cs`
- `src/Neo/Persistence/StateRecorderSupabase.JsonCsvUpload.*.cs`

## 11.1 Binary snapshots (`.bin`, NSBR format)

When the effective upload mode includes `Binary` and Supabase Storage is configured, the recorder uploads:
- `block-{blockIndex}.bin` to bucket `NEO_STATE_RECORDER__SUPABASE_BUCKET` (default: `block-state`)

It uses an HTTP `PUT` with `x-upsert=true` so re-syncs overwrite existing files:
- `src/Neo/Persistence/StateRecorderSupabase.BinaryUpload.cs`

Binary format is defined by the writer:
- `src/Neo/Persistence/StateRecorderSupabase.BinaryUpload.PayloadBuilders.Write.cs`

Format:
- Header: `[Magic "NSBR": 4 bytes] [Version: uint16] [BlockIndex: uint32] [EntryCount: int32]`
- Entries: `[ContractHash: 20 bytes] [KeyLen: uint16] [KeyBytes] [ValueLen: int32] [ValueBytes] [ReadOrder: int32]`

Important nuance: `KeyBytes` is a serialized `StorageKey`:
- `[ContractId: int32 little-endian] + [StorageKey.Key bytes]`

This is the format consumed by `StateReplay` (see section 12).

## 11.2 Optional JSON/CSV snapshots (`.json` / `.csv`)

When `NEO_STATE_RECORDER__UPLOAD_AUX_FORMATS=true`, the uploader additionally writes:
- `block-{blockIndex}.json`
- `block-{blockIndex}.csv`

The JSON format includes rich per-read metadata (contract id/hash, manifest name, tx hash attribution, source) and is intended for debugging/inspection:
- `src/Neo/Persistence/StateRecorderSupabase.JsonCsvUpload.PayloadBuilders.Json.cs`

Operationally, these formats are disabled by default because they create a large number of files and can be sizeable on “hot” blocks.

## 11.3 Empty-read blocks

If a block produced **zero recorded reads**, the plugin still upserts the `blocks` row (so the UI can find the block), but it avoids binary snapshot uploads to prevent file explosion:
- `src/Plugins/BlockStateIndexer/BlockStateIndexerPlugin.Handlers.Committed.StorageReads.cs`
