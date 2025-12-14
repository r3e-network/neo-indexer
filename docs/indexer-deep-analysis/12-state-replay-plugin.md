# 12. StateReplay plugin (replaying blocks against captured state)

The `StateReplay` plugin is a debugging tool: it can replay a given block using a captured key/value snapshot (from a file or Supabase) to help diagnose determinism/state issues.

Where this is implemented:
- `src/Plugins/StateReplay/StateReplayPlugin.Replay.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Commands.BlockState.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Commands.Binary.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Commands.Supabase.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Supabase.Replay.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Supabase.Download.cs`
- `src/Plugins/StateReplay/BinaryFormatReader.cs`

## 12.1 Replay algorithm

`ReplayBlock(Block block, StoreCache snapshot)` follows Neo’s persistence lifecycle at a high level:
1. Runs native `OnPersist` via a syscall script.
2. Executes each transaction script:
   - If `HALT`, commits the snapshot changes.
   - If `FAULT`, discards changes and resets the snapshot to the original base (by cloning).
3. Runs native `PostPersist` via a syscall script.

This is a deliberately “pure” replay: it does not depend on the node’s live storage, only on the provided snapshot and the block’s transactions.

## 12.2 Snapshot inputs

StateReplay supports multiple snapshot sources:

- **JSON snapshot file** (`replay block-state <file>`):
  - Expects the same “block JSON export” shape that `StateRecorderSupabase` can upload (`block-<index>.json`)
  - Reads base64 `key`/`value` and casts `key` bytes into a `StorageKey`

- **Binary snapshot file** (`replay block-binary <file>`):
  - Reads NSBR (`block-<index>.bin`) via `BinaryFormatReader`
  - Loads entries into a `MemoryStore` snapshot

- **Supabase PostgREST** (`replay supabase <blockIndex>`):
  - Pages through `storage_reads` ordered by `read_order` and reconstructs `StorageKey { Id, Key }`
  - Useful when you didn’t upload storage files but did persist `storage_reads`

- **Supabase Storage download** (`replay download <blockIndex>`):
  - Downloads `block-<index>.bin` into a local cache directory (`StateReplay.json` `CacheDirectory`)

## 12.3 Current limitations

`replay compare <snapshotFile>` replays the block against the snapshot and produces a *read coverage* report (hit/miss keys at the store layer):
- `src/Plugins/StateReplay/StateReplayPlugin.Commands.Compare.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Compare.cs`
- `src/Plugins/StateReplay/StateReplayPlugin.Compare.Snapshot.cs`
- `src/Plugins/StateReplay/ReadCapturingStoreSnapshot.cs`

This is useful to answer questions like:
- “Did the replay attempt to read a key that is missing from my snapshot?”
- “How much of the snapshot was actually used?”
- “Did the replay read keys that were not present in the snapshot (e.g., keys created during replay)?”

It still does **not** perform a full “live execution vs replay” diff (events, ordering, VM state transitions, storage *values*, etc.); it is primarily a snapshot *coverage* and missing-data diagnostic.
