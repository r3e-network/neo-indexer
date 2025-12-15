# `getstoragewritestats`

Aggregates storage write volume over a block range (grouped by `contract_hash`).

Back to [stats index](../06-stats.md).

## Options Object

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

## Example Response

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

