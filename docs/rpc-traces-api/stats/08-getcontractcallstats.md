# `getcontractcallstats`

Aggregates contract call usage (callee/caller/method) over a block range.  
This is implemented as a Supabase RPC function `get_contract_call_stats` and can be called directly from the frontend via supabase‑js `rpc`.

Back to [stats index](../06-stats.md).

## Options Object

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

