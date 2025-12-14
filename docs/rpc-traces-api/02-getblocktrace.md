# `getblocktrace`

Returns per-transaction execution results plus opcode, syscall, contract-call, storage-write, notification, and runtime log traces for a given block.

Collections included:
- `transactionResults`
- `opcodes`
- `syscalls`
- `contractCalls`
- `storageReads`
- `storageWrites`
- `notifications`
- `logs`

Notes:
- `storageReads.items[*]` are *deduped per key per block* (first-observed value for each `(contract_id, key)` pair).
- `storageReads.items[*].contractHash` / `manifestName` are included when `contract_id` resolves in the `contracts` reference table.
- `opcodes.items[*].gasConsumed` is the opcode fee for that instruction (in datoshi).
- `syscalls.items[*].gasCost` is the syscall fee (including any dynamic fees charged inside the handler).
- `contractCalls.items[*].success` is `false` when the callee context unwinds due to an exception (including exceptions that are caught by the caller). For the overall transaction outcome, use `transactionResults`.
- `storageWrites.items[*].isDelete` is `true` for delete operations; deletes set `newValueBase64` to an empty string (use `isDelete` to disambiguate from writes of an empty byte array).
- `logs.items[*].message` is emitted by `System.Runtime.Log`.

## Parameters

1. `blockHashOrIndex` (string|number): block hash (`0x...`) or block index.
2. `options` (object, optional): see `docs/rpc-traces-api/01-overview.md` for Trace Request Options.

## Example Request

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "getblocktrace",
  "params": [777, { "limit": 500, "offset": 0 }]
}
```

Response rows may include `syscallHash`, `category`, and `gasBase` when the `syscall_names`
reference table is populated. `gasCost` reflects the actual fee consumed by the syscall (including
dynamic fees charged inside the handler), while `gasBase` is the fixed base price from `syscall_names`.

## Example Response

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "blockIndex": 777,
    "blockHash": "0x1234...",
    "limit": 500,
    "offset": 0,
    "opcodes": {
      "total": 1200,
      "items": [
        {
          "blockIndex": 777,
          "transactionHash": "0xtx...",
          "contractHash": "0xcontract...",
          "instructionPointer": 0,
          "opcode": 16,
          "opcodeName": "PUSH1",
          "operand": "AQ==",
          "gasConsumed": 1000000,
          "stackDepth": 1,
          "traceOrder": 0
        }
      ]
    },
    "syscalls": {
      "total": 42,
      "items": [
        {
          "blockIndex": 777,
          "transactionHash": "0xtx...",
          "contractHash": "0xcontract...",
          "syscallHash": "31E85D92",
          "syscallName": "System.Storage.Get",
          "gasCost": 32768,
          "traceOrder": 0
        }
      ]
    },
    "contractCalls": {
      "total": 5,
      "items": [
        {
          "blockIndex": 777,
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
    },
    "logs": {
      "total": 2,
      "items": [
        {
          "blockIndex": 777,
          "transactionHash": "0xtx...",
          "logOrder": 0,
          "contractHash": "0xcontract...",
          "message": "hello world"
        }
      ]
    }
  }
}
```
