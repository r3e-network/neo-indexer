# `getstoragereadstats`

Aggregates `storage_reads` volume over a block range (grouped by `contract_hash`).

Back to [stats index](../06-stats.md).

Notes:
- `storage_reads` is deduped per `(block_index, contract_id, key_base64)`, so `readCount` is effectively “unique keys first-observed” in the range.

## Options Object

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

## Example Response

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

