# Neo Indexer (Supabase)

Runs a Neo N3 node (`neo-cli`) plus indexing plugins that write canonical block/trace/state data into **Supabase Postgres**.

- **Indexer writes**: Supabase `service_role` key (bypasses RLS)
- **Frontend reads**: Supabase `anon` key with strict RLS read-only access

**Quick links**
- `docs/DEPLOYMENT-SUPABASE-MAINNET.md`
- `docs/ARCHITECTURE-neo-indexer-v2.md`
- `docs/RPC-TRACES-API.md`
- `docs/INDEXER-DEEP-ANALYSIS.md`

## What’s in this repo

- `src/Neo.CLI`: node runner (`neo-cli`)
- `src/Plugins/BlockStateIndexer`: indexing + Supabase upload (includes `RecordingStore` wrapper)
- `src/Plugins/RpcServer`: RPC server + trace RPC extensions
- `src/Plugins/StateReplay`: replay/debug tooling for captured state
- `tools/CreateTables`: Supabase admin tool (migrations, partitions, pruning)
- `frontend`: read-only UI for indexed data

## Quick start (mainnet)

1. Configure environment:
   - `cp .env.example .env` and fill in Supabase settings
2. Build:
   - `dotnet build neo.sln -c Release`
3. Create/upgrade DB schema:
   - `dotnet run -c Release --project tools/CreateTables migrate`
4. Run:
   - `./run-mainnet.sh`

## Notes

- To record storage reads, set `"Storage.Engine": "RecordingStore"` in `src/Neo.CLI/config.mainnet.json` (or your `config.json`) and ensure `NEO_STATE_RECORDER__BASE_STORE_PROVIDER` matches the underlying store provider (default: `LevelDBStore`).
- Plugin configs are generated/loaded from `plugins/*/*.json` when running `neo-cli` (see `src/Plugins/*/*.json` for defaults).

## Credits

This repository is a fork of `neo-project/neo`, focused on running an indexer and storing derived data in Supabase.
