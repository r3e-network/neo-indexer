# Trace RPC API (overview)

This document describes the execution‑trace query surfaces added by Block State Indexer v2.

There are two ways to consume traces:

1. **Supabase‑only (recommended / used by the frontend)**  
   The web UI reads trace tables directly from Supabase Postgres and calls Supabase RPC
   functions for aggregated stats:
   - `get_syscall_stats(start_block, end_block, ...)`
   - `get_opcode_stats(start_block, end_block, ...)`
   - `get_contract_call_stats(start_block, end_block, ...)`
   - `get_runtime_log_stats(start_block, end_block, ...)`
   - `get_block_stats(start_block, end_block, ...)`
   - `get_notification_stats(start_block, end_block, ...)`

   Optional filter parameters for Supabase RPC are prefixed with `p_` (to avoid PL/pgSQL name collisions), e.g.:
   - `get_syscall_stats(..., p_contract_hash, p_transaction_hash, p_syscall_name, limit_rows, offset_rows)`
   - `get_opcode_stats(..., p_contract_hash, p_transaction_hash, p_opcode, p_opcode_name, limit_rows, offset_rows)`
   - `get_contract_call_stats(..., p_callee_hash, p_caller_hash, p_method_name, limit_rows, offset_rows)`

   **Guardrails:** when called with public `anon` / `authenticated` keys, all stats RPCs enforce:
   - max block range of **500,000 blocks per request**
   - `limit_rows` clamped to **1000**

   Service role callers are exempt.

2. **Neo JSON‑RPC proxy (optional)**  
   The `RpcServer.Traces` plugin exposes `getblocktrace`, `gettransactiontrace`,
   `gettransactionresult`, `getcontractcalls`, `getcontractcallstats`, `getsyscallstats`,
   `getopcodestats`, `getlogstats`, `getblockstats`, and `getnotificationstats`. These are thin HTTPS
   proxies to Supabase PostgREST and are useful for non‑browser clients.

## RPC Proxy Configuration

`RpcServer.Traces` reads the Supabase URL/key from the state recorder env vars:

- `NEO_STATE_RECORDER__SUPABASE_URL`
- `NEO_STATE_RECORDER__SUPABASE_KEY`

If your Neo JSON‑RPC endpoint is reachable by untrusted clients, prefer using an `anon` key for trace queries
so Supabase RLS/RPC guardrails apply:

- `NEO_RPC_TRACES__SUPABASE_KEY` (optional override for trace RPC endpoints only)

To reduce load on Supabase under high traffic, you can also limit concurrent Supabase requests issued by
trace RPC endpoints:

- `NEO_RPC_TRACES__MAX_CONCURRENCY` (default `16`)

## Common Concepts

### Trace Request Options

Most trace queries accept an optional options object:

```json
{
  "limit": 1000,
  "offset": 0,
  "transactionHash": "0x..."   // only for getblocktrace
}
```

- `limit` (optional): number of rows to return per collection. Default `1000`, max `5000`.
- `offset` (optional): pagination offset, default `0`.
- `transactionHash` (optional): filter to a single transaction within a block.

### Trace Collections

Responses that include trace collections wrap them as:

```json
{
  "total": 1234,
  "items": [ ... ]
}
```

The `total` value reflects the total matching rows in Supabase (if PostgREST count headers are enabled) or the returned count otherwise.

