# `gettransactiontrace`

Returns traces for a single transaction.

## Parameters

1. `txHash` (string): transaction hash (`0x...`).
2. `options` (object, optional): Trace Request Options (without `transactionHash`). See `docs/rpc-traces-api/01-overview.md`.

## Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "gettransactiontrace",
  "params": ["0xtxhash", { "limit": 2000 }]
}
```

## Response Notes

Same shape as `getblocktrace`, but includes `transactionHash` at the top‑level and only rows for that transaction (including a single `transactionResults` row when available).

## Example Response

```json
{
  "blockIndex": 777,
  "blockHash": "0x1234...",
  "transactionHash": "0xtxhash",
  "limit": 1000,
  "offset": 0,
  "transactionResults": { "total": 0, "items": [] },
  "opcodes": { "total": 0, "items": [] },
  "syscalls": { "total": 0, "items": [] },
  "contractCalls": { "total": 0, "items": [] },
  "storageWrites": { "total": 0, "items": [] },
  "notifications": { "total": 0, "items": [] },
  "logs": { "total": 0, "items": [] }
}
```
