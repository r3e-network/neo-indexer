# `getnotificationstats`

Aggregates `System.Runtime.Notify` event volume over a block range (grouped by `contract_hash` + `event_name`).

Back to [stats index](../06-stats.md).

## Options Object

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

## Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "getnotificationstats",
  "params": [{ "startBlock": 5000000, "endBlock": 5001000, "eventName": "Transfer", "limit": 50 }]
}
```

## Example Response

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

