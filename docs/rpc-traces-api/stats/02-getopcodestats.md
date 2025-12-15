# `getopcodestats`

Aggregates opcode usage over a block range.

Back to [stats index](../06-stats.md).

## Options Object

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

## Example Response

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

