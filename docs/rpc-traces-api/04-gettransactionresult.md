# `gettransactionresult`

Returns the per‑transaction execution outcome row from `transaction_results` (VM state, GAS consumed, fault details, result stack, and per‑tx trace counts).

This is complementary to `gettransactiontrace`:
- `gettransactiontrace` returns the detailed trace rows.
- `gettransactionresult` returns the final outcome + summary counters.

## Parameters

1. `txHash` (string): transaction hash (`0x...`).

## Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "gettransactionresult",
  "params": ["0xtxhash"]
}
```

## Example Response

If the transaction exists on-chain but the indexer has not uploaded the row yet, the RPC proxy returns:

```json
{
  "indexed": false,
  "blockIndex": 777,
  "blockHash": "0x1234...",
  "transactionHash": "0xtxhash"
}
```

Otherwise, it returns the stored execution outcome:

```json
{
  "indexed": true,
  "blockIndex": 777,
  "blockHash": "0x1234...",
  "transactionHash": "0xtxhash",
  "vmState": 1,
  "vmStateName": "HALT",
  "success": true,
  "gasConsumed": 123456,
  "opcodeCount": 1000,
  "syscallCount": 20,
  "contractCallCount": 5,
  "storageWriteCount": 3,
  "notificationCount": 2,
  "logCount": 1,
  "resultStack": [ ... ]
}
```

