# Trace RPC API

This document describes the execution-trace RPC extensions added by the Block State Indexer v2. These methods query trace tables in Supabase (or any PostgREST‑compatible backend) and return normalized JSON suitable for the frontend trace browser.

All methods are exposed via the standard Neo JSON‑RPC endpoint. Method names are lowercase versions of the C# handlers.

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

- `limit` (optional): number of rows to return per trace type. Default `1000`, max `5000`.
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

Returns opcode, syscall, and contract‑call traces for a given block.

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

Same shape as `getblocktrace`, but includes `transactionHash` at the top‑level and only rows for that transaction.

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
      "syscallName": "System.Storage.Get",
      "callCount": 340,
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

## Errors

Errors follow the standard Neo RPC error format. Common errors include:

- `InvalidParams`: missing or malformed hashes, block indices, or option values.
- `UnknownBlock`: block does not exist (for index/hash lookups).
- `UnknownTransaction`: transaction not found.
- `InternalServerError`: Supabase query failed or trace storage is not configured.

