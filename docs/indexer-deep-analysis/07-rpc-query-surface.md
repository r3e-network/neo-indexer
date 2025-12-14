# 7. RPC query surface (reading traces from Supabase)

The RpcServer plugin offers trace-related endpoints that query Supabase directly:
- `src/Plugins/RpcServer/RpcServer.Traces.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Types.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Supabase.Settings.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Supabase.Client.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Supabase.Http.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Endpoints.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Endpoints.ContractCalls.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Endpoints.Stats.Syscalls.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Endpoints.Stats.OpCodes.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Endpoints.Stats.ContractCalls.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Parsing.BlockIdentifier.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Parsing.TraceRequestOptions.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Parsing.ContractCalls.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Parsing.Stats.cs`
- `src/Plugins/RpcServer/RpcServer.Traces.Parsing.Helpers.cs`

Key behaviors:
- Uses Supabase PostgREST reads for trace tables (and Supabase RPC functions for stats).
- Also exposes per-transaction outcome rows from `transaction_results` via `gettransactionresult`.
- `getblocktrace` / `gettransactiontrace` also include `transaction_results`, `storage_writes`, `notifications`, and `logs` (from `runtime_logs`) in the returned payload.
- Supports optional per-request limits/offsets with caps to protect Supabase.
- Supports an override key `NEO_RPC_TRACES__SUPABASE_KEY` (recommended for public RPC deployments so you can use an anon key + RLS).
- Has its own concurrency gate (`NEO_RPC_TRACES__MAX_CONCURRENCY`) to avoid stampedes.
