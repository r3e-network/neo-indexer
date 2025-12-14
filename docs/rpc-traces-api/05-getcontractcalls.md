# `getcontractcalls`

Returns the contract‑call graph edges for a given contract over a block range.

## Parameters

1. `contractHash` (string): script hash (`0x...`).
2. `options` (object, optional):

```json
{
  "startBlock": 0,
  "endBlock": 1000,
  "transactionHash": "0x...",
  "role": "caller" | "callee" | "any",
  "limit": 1000,
  "offset": 0
}
```

## Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "getcontractcalls",
  "params": ["0xcontract", { "startBlock": 5000000, "endBlock": 5001000 }]
}
```

## Example Response

```json
{
  "contractHash": "0xcontract",
  "startBlock": 5000000,
  "endBlock": 5001000,
  "limit": 1000,
  "offset": 0,
  "total": 12,
  "calls": [
    {
      "blockIndex": 5000001,
      "transactionHash": "0xtx...",
      "callerHash": "0xcaller...",
      "calleeHash": "0xcallee...",
      "methodName": "transfer",
      "callDepth": 1,
      "traceOrder": 0,
      "success": true,
      "gasConsumed": 200000
    }
  ]
}
```

