# Stats endpoints

All stats endpoints support two parameter patterns:

1) Positional parameters:
- `startBlock` (number)
- `endBlock` (number)
- `options` (object, optional)

2) A single options object:
- `startBlock` (number)
- `endBlock` (number)
- plus endpoint-specific filters

## `getsyscallstats`

Aggregates syscall usage over a block range.

### Options Object

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

## `getopcodestats`

Aggregates opcode usage over a block range.

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

## `getlogstats`

Aggregates `System.Runtime.Log` volume over a block range (grouped by `contract_hash`).

### Options Object

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "contractHash": "0x...",
  "transactionHash": "0x...",
  "limit": 100,
  "offset": 0
}
```

### Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "getlogstats",
  "params": [{ "startBlock": 5000000, "endBlock": 5001000, "limit": 100 }]
}
```

### Example Response

```json
{
  "startBlock": 5000000,
  "endBlock": 5001000,
  "limit": 100,
  "offset": 0,
  "total": 123,
  "stats": [
    { "contractHash": "0x...", "logCount": 42, "firstBlock": 5000001, "lastBlock": 5000999 }
  ]
}
```

## `getblockstats`

Fetches per-block aggregates from `block_stats` over a block range (including `logCount`).

### Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "getblockstats",
  "params": [{ "startBlock": 5000000, "endBlock": 5000100, "limit": 100 }]
}
```

### Example Response

```json
{
  "startBlock": 5000000,
  "endBlock": 5000100,
  "limit": 100,
  "offset": 0,
  "total": 101,
  "stats": [
    {
      "blockIndex": 5000000,
      "transactionCount": 123,
      "totalGasConsumed": 4567890,
      "opcodeCount": 120000,
      "syscallCount": 4000,
      "contractCallCount": 800,
      "storageReadCount": 900,
      "storageWriteCount": 120,
      "notificationCount": 50,
      "logCount": 10
    }
  ]
}
```

## `getnotificationstats`

Aggregates `System.Runtime.Notify` event volume over a block range (grouped by `contract_hash` + `event_name`).

### Options Object

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "contractHash": "0x...",
  "transactionHash": "0x...",
  "eventName": "Transfer",
  "limit": 100,
  "offset": 0
}
```

### Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "getnotificationstats",
  "params": [{ "startBlock": 5000000, "endBlock": 5001000, "eventName": "Transfer", "limit": 50 }]
}
```

### Example Response

```json
{
  "startBlock": 5000000,
  "endBlock": 5001000,
  "limit": 50,
  "offset": 0,
  "total": 123,
  "stats": [
    { "contractHash": "0x...", "eventName": "Transfer", "notificationCount": 42, "firstBlock": 5000001, "lastBlock": 5000999 }
  ]
}
```

## `getstoragewritestats`

Aggregates storage write volume over a block range (grouped by `contract_hash`).

### Options Object

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "contractHash": "0x...",
  "transactionHash": "0x...",
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
    { "contractHash": "0x...", "writeCount": 120, "deleteCount": 5, "firstBlock": 10, "lastBlock": 999 }
  ]
}
```

## `getstoragereadstats`

Aggregates `storage_reads` volume over a block range (grouped by `contract_hash`).

Notes:
- `storage_reads` is deduped per `(block_index, contract_id, key_base64)`, so `readCount` is effectively “unique keys first-observed” in the range.

### Options Object

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "contractHash": "0x...",
  "transactionHash": "0x...",
  "source": "System.Storage.Get",
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
    { "contractHash": "0x...", "readCount": 900, "firstBlock": 10, "lastBlock": 999 }
  ]
}
```

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
