# `getlogstats`

Aggregates `System.Runtime.Log` volume over a block range (grouped by `contract_hash`).

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

## Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "getlogstats",
  "params": [{ "startBlock": 5000000, "endBlock": 5001000, "limit": 100 }]
}
```

## Example Response

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

