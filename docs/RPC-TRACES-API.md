# Trace RPC API

This document describes the execution‑trace query surfaces added by Block State Indexer v2.

There are two ways to consume traces:

1. **Supabase‑only (recommended / used by the frontend)**  
   The web UI reads trace tables directly from Supabase Postgres and calls two Supabase RPC
   functions for aggregated stats:
   - `get_syscall_stats(start_block, end_block, ...)`
   - `get_opcode_stats(start_block, end_block, ...)`
   - `get_contract_call_stats(start_block, end_block, ...)`

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
   `gettransactionresult`, `getcontractcalls`, `getcontractcallstats`, `getsyscallstats`, and `getopcodestats`. These are thin HTTPS
   proxies to Supabase PostgREST and are useful for non‑browser clients.

### RPC Proxy Configuration

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

---

## `getblocktrace`

Returns per-transaction execution results plus opcode, syscall, contract-call, storage-write, and notification traces for a given block.

Collections included:
- `transactionResults`
- `opcodes`
- `syscalls`
- `contractCalls`
- `storageWrites`
- `notifications`

Notes:
- `opcodes.items[*].gasConsumed` is the opcode fee for that instruction (in datoshi).
- `syscalls.items[*].gasCost` is the syscall fee (including any dynamic fees charged inside the handler).
- `contractCalls.items[*].success` is `false` when the callee context unwinds due to an exception (including exceptions that are caught by the caller). For the overall transaction outcome, use `transactionResults`.
- `storageWrites.items[*].isDelete` is `true` for delete operations; deletes set `newValueBase64` to an empty string (use `isDelete` to disambiguate from writes of an empty byte array).

### Parameters

1. `blockHashOrIndex` (string|number): block hash (`0x...`) or block index.
2. `options` (object, optional): Trace Request Options.

### Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "getblocktrace",
  "params": [777, { "limit": 500, "offset": 0 }]
}
```

Response rows may include `syscallHash`, `category`, and `gasBase` when the `syscall_names`
reference table is populated. `gasCost` reflects the actual fee consumed by the syscall (including
dynamic fees charged inside the handler), while `gasBase` is the fixed base price from `syscall_names`.

### Example Response

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "blockIndex": 777,
    "blockHash": "0x1234...",
    "limit": 500,
    "offset": 0,
    "opcodes": {
      "total": 1200,
      "items": [
        {
          "blockIndex": 777,
          "transactionHash": "0xtx...",
          "contractHash": "0xcontract...",
          "instructionPointer": 0,
          "opcode": 16,
          "opcodeName": "PUSH1",
          "operand": "AQ==",
          "gasConsumed": 1000000,
          "stackDepth": 1,
          "traceOrder": 0
        }
      ]
    },
    "syscalls": {
      "total": 42,
      "items": [
        {
          "blockIndex": 777,
          "transactionHash": "0xtx...",
          "contractHash": "0xcontract...",
          "syscallHash": "31E85D92",
          "syscallName": "System.Storage.Get",
          "gasCost": 32768,
          "traceOrder": 0
        }
      ]
    },
    "contractCalls": {
      "total": 5,
      "items": [
        {
          "blockIndex": 777,
          "transactionHash": "0xtx...",
          "callerHash": "0xcaller...",
          "calleeHash": "0xcallee...",
          "methodName": "transfer",
          "callDepth": 1,
          "traceOrder": 0,
          "success": true,
          "gasConsumed": 200000
        }
      ]
    }
  }
}
```

---

## `gettransactiontrace`

Returns traces for a single transaction.

### Parameters

1. `txHash` (string): transaction hash (`0x...`).
2. `options` (object, optional): Trace Request Options (without `transactionHash`).

### Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "gettransactiontrace",
  "params": ["0xtxhash", { "limit": 2000 }]
}
```

### Response Notes

Same shape as `getblocktrace`, but includes `transactionHash` at the top‑level and only rows for that transaction (including a single `transactionResults` row when available).

---

## `gettransactionresult`

Returns the per‑transaction execution outcome row from `transaction_results` (VM state, GAS consumed, fault details, result stack, and per‑tx trace counts).

This is complementary to `gettransactiontrace`:
- `gettransactiontrace` returns the detailed trace rows.
- `gettransactionresult` returns the final outcome + summary counters.

### Parameters

1. `txHash` (string): transaction hash (`0x...`).

### Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "gettransactionresult",
  "params": ["0xtxhash"]
}
```

### Example Response

If the transaction exists on-chain but the indexer has not uploaded the row yet, the RPC proxy returns:

```json
{
  "indexed": false,
  "blockIndex": 777,
  "blockHash": "0x1234...",
  "transactionHash": "0xtxhash"
}
```

Otherwise, it returns the stored execution outcome:

```json
{
  "indexed": true,
  "blockIndex": 777,
  "blockHash": "0x1234...",
  "transactionHash": "0xtxhash",
  "vmState": 1,
  "vmStateName": "HALT",
  "success": true,
  "gasConsumed": 123456,
  "opcodeCount": 1000,
  "syscallCount": 20,
  "contractCallCount": 5,
  "storageWriteCount": 3,
  "notificationCount": 2,
  "resultStack": [ ... ]
}
```

---

## `getcontractcalls`

Returns the contract‑call graph edges for a given contract over a block range.

### Parameters

1. `contractHash` (string): script hash (`0x...`).
2. `options` (object, optional):

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "transactionHash": "0x...",
  "role": "caller" | "callee" | "any",
  "limit": 1000,
  "offset": 0
}
```

### Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "getcontractcalls",
  "params": ["0xcontract", { "startBlock": 5000000, "endBlock": 5001000 }]
}
```

### Example Response

```json
{
  "contractHash": "0xcontract",
  "startBlock": 5000000,
  "endBlock": 5001000,
  "limit": 1000,
  "offset": 0,
  "total": 12,
  "calls": [
    {
      "blockIndex": 5000001,
      "transactionHash": "0xtx...",
      "callerHash": "0xcaller...",
      "calleeHash": "0xcallee...",
      "methodName": "transfer",
      "callDepth": 1,
      "traceOrder": 0,
      "success": true,
      "gasConsumed": 200000
    }
  ]
}
```

---

## `getsyscallstats`

Aggregates syscall usage over a block range.

### Parameters

This method supports positional parameters:

1. `startBlock` (number)
2. `endBlock` (number)
3. `options` (object, optional)

Or a single options object:

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "contractHash": "0x...",
  "transactionHash": "0x...",
  "syscallName": "System.Storage.Get",
  "limit": 100,
  "offset": 0
}
```

### Example Response

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "limit": 100,
  "offset": 0,
  "total": 2,
	  "stats": [
	    {
	      "syscallHash": "31E85D92",
	      "syscallName": "System.Storage.Get",
	      "category": "Storage",
	      "callCount": 340,
	      "gasBase": 32768,
	      "totalGasCost": 11141120,
	      "averageGasCost": 32768,
	      "minGasCost": 32768,
      "maxGasCost": 32768,
      "firstBlock": 10,
      "lastBlock": 999
    }
  ]
}
```

---

## `getopcodestats`

Aggregates opcode usage over a block range. Supports the same parameter patterns as `getsyscallstats`.

### Options Object

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "contractHash": "0x...",
  "transactionHash": "0x...",
  "opcode": 16,
  "opcodeName": "PUSH1",
  "limit": 100,
  "offset": 0
}
```

### Example Response

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "limit": 100,
  "offset": 0,
  "total": 1,
  "stats": [
    {
      "opcode": 16,
      "opcodeName": "PUSH1",
      "callCount": 12000,
      "totalGasConsumed": 400000000,
      "averageGasConsumed": 33333.3,
      "minGasConsumed": 8,
      "maxGasConsumed": 32768,
      "firstBlock": 0,
      "lastBlock": 1000
    }
  ]
}
```

---

## `getcontractcallstats`

Aggregates contract call usage (callee/caller/method) over a block range.  
This is implemented as a Supabase RPC function `get_contract_call_stats` and can be called
directly from the frontend via supabase‑js `rpc`.

### Options Object

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "calleeHash": "0x...",        // optional
  "callerHash": "0x...",        // optional
  "methodName": "transfer",     // optional
  "limit": 100,
  "offset": 0
}
```

### Example Response

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "limit": 100,
  "offset": 0,
  "total": 2,
  "stats": [
    {
      "calleeHash": "0xcallee...",
      "callerHash": "0xcaller...",
      "methodName": "transfer",
      "callCount": 500,
      "successCount": 495,
      "failureCount": 5,
      "totalGasConsumed": 123456789,
      "averageGasConsumed": 246913.5,
      "firstBlock": 10,
      "lastBlock": 999
    }
  ]
}
```

---

## Errors

Errors follow the standard Neo RPC error format. Common errors include:

- `InvalidParams`: missing or malformed hashes, block indices, or option values.
- `UnknownBlock`: block does not exist (for index/hash lookups).
- `UnknownTransaction`: transaction not found.
- `InternalServerError`: Supabase query failed or trace storage is not configured.
