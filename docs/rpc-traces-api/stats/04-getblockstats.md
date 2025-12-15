# `getblockstats`

Fetches per-block aggregates from `block_stats` over a block range (including `logCount`).

Back to [stats index](../06-stats.md).

## Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "getblockstats",
  "params": [{ "startBlock": 5000000, "endBlock": 5000100, "limit": 100 }]
}
```

## Example Response

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

