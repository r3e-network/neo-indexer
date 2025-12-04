# Neo.Plugins.DeepLogger

`neo-cli` persistence plugin that replays transactions to capture opcode traces, writing CSV files for ingestion.

## Build
```bash
dotnet restore
dotnet build -c Release
```

Copy the built DLLs into `neo-cli/Plugins/DeepLogger`.

## Configuration
- `DEEPLOGGER_LOG_DIR` (env): defaults to `/neo-data/logs`.
- `DEEPLOGGER_ROTATE_BLOCKS` (env): defaults to `1000` blocks per file.
  - Filenames: `blocks_<i>.csv`, `txs_<i>.csv`, `trace_<i>.csv` where `i = blockIndex / rotateBlocks`.
- CSV: UTF-8, buffered 64 KiB writers.

## CSV Layouts
- `blocks_*.csv`: `block_index,hash,timestamp,tx_count`
- `txs_*.csv`: `tx_hash,block_index,sender,sys_fee,net_fee`
- `trace_*.csv`: `tx_hash,block_index,step_order,contract_hash,opcode,syscall,gas_consumed,stack_top`
  - `stack_top` sanitized (commas -> `;`, newlines removed, truncated to 64 chars).

## Notes
- Uses `ApplicationEngine.Create` to replay with `TriggerType.Application` and the transaction’s system fee.
- Skips transactions without an `ApplicationExecuted` entry.
- Closes writers on rotation and dispose to avoid file locks.
