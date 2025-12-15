# `getsyscallstats`

Aggregates syscall usage over a block range.

Back to [stats index](../06-stats.md).

## Options Object

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

## Example Response

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

