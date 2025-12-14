# 14. SQL analytics examples (results + traces)

The indexer writes a “thin but complete” transaction outcome row to `transaction_results` and (optionally) detailed traces to the partitioned trace tables (`opcode_traces`, `syscall_traces`, `contract_calls`, `storage_writes`, `notifications`, `runtime_logs`).

This enables most analytics to be done with plain SQL (or Supabase views/RPCs) without re-executing blocks.

## 14.1 Transaction outcome queries

Most common starting point: find failures and heavy transactions.

```sql
-- Faulted transactions (last 10k blocks)
SELECT block_index, tx_hash, vm_state_name, gas_consumed, fault_exception
FROM transaction_results
WHERE block_index >= (SELECT MAX(block_index) FROM blocks) - 10000
  AND success = false
ORDER BY block_index DESC
LIMIT 200;
```

```sql
-- Top gas consumers (last 10k blocks)
SELECT block_index, tx_hash, gas_consumed, success
FROM transaction_results
WHERE block_index >= (SELECT MAX(block_index) FROM blocks) - 10000
ORDER BY gas_consumed DESC
LIMIT 200;
```

## 14.2 Opcode / syscall frequency

```sql
-- Most common opcodes in a block range
SELECT opcode_name, COUNT(*) AS n
FROM opcode_traces
WHERE block_index BETWEEN 5000000 AND 5010000
GROUP BY opcode_name
ORDER BY n DESC
LIMIT 50;
```

```sql
-- Most common syscalls in a block range
SELECT syscall_name, COUNT(*) AS n
FROM syscall_traces
WHERE block_index BETWEEN 5000000 AND 5010000
GROUP BY syscall_name
ORDER BY n DESC
LIMIT 50;
```

Tip: for large ranges, always constrain by `block_index` to take advantage of partition pruning.

## 14.3 Contract call graph (who calls who)

```sql
-- “Edge” counts between contracts (caller → callee)
SELECT caller_hash, callee_hash, COUNT(*) AS calls
FROM contract_calls
WHERE block_index BETWEEN 5000000 AND 5010000
GROUP BY caller_hash, callee_hash
ORDER BY calls DESC
LIMIT 100;
```

```sql
-- Top called methods for a given callee in a range
SELECT callee_hash, method_name, COUNT(*) AS calls
FROM contract_calls
WHERE block_index BETWEEN 5000000 AND 5010000
  AND callee_hash = '0x...'
GROUP BY callee_hash, method_name
ORDER BY calls DESC
LIMIT 100;
```

## 14.4 Notifications / events

```sql
-- Event frequency per contract
SELECT contract_hash, event_name, COUNT(*) AS n
FROM notifications
WHERE block_index BETWEEN 5000000 AND 5010000
GROUP BY contract_hash, event_name
ORDER BY n DESC
LIMIT 200;
```

```sql
-- Filter by event name (example)
SELECT block_index, tx_hash, contract_hash, state_json
FROM notifications
WHERE block_index BETWEEN 5000000 AND 5010000
  AND event_name = 'Transfer'
ORDER BY block_index DESC
LIMIT 200;
```

```sql
-- Runtime.Log volume per contract
SELECT contract_hash, COUNT(*) AS n
FROM runtime_logs
WHERE block_index BETWEEN 5000000 AND 5010000
GROUP BY contract_hash
ORDER BY n DESC
LIMIT 200;
```

## 14.5 Correlating outcomes with trace volume

Because `transaction_results` stores per-tx counts (and is much smaller than the per-opcode table), it’s the right table to use for many “volume” queries.

```sql
-- Average opcodes per tx, grouped by success/failure
SELECT success, AVG(opcode_count) AS avg_opcodes, AVG(gas_consumed) AS avg_gas
FROM transaction_results
WHERE block_index BETWEEN 5000000 AND 5010000
GROUP BY success;
```

```sql
-- Average Runtime.Log messages per tx, grouped by success/failure
SELECT success, AVG(log_count) AS avg_logs
FROM transaction_results
WHERE block_index BETWEEN 5000000 AND 5010000
GROUP BY success;
```

```sql
-- Join tx outcome with contract calls for “fault rate by callee”
SELECT c.callee_hash,
       COUNT(*) AS calls,
       SUM(CASE WHEN r.success THEN 0 ELSE 1 END) AS faulted_txs
FROM contract_calls c
JOIN transaction_results r
  ON r.block_index = c.block_index AND r.tx_hash = c.tx_hash
WHERE c.block_index BETWEEN 5000000 AND 5010000
GROUP BY c.callee_hash
ORDER BY faulted_txs DESC, calls DESC
LIMIT 100;
```

## 14.6 Storage write diffs (before/after)

`storage_writes` stores `old_value_base64` (pre-write value at the time of the write) and `new_value_base64` (value being written). It also stores `is_delete` to disambiguate deletes from writes that set an empty byte array.

```sql
-- Contracts with the most distinct keys written in a block range
SELECT contract_hash,
       COUNT(DISTINCT key_base64) AS keys_written
FROM storage_writes
WHERE block_index BETWEEN 5000000 AND 5010000
GROUP BY contract_hash
ORDER BY keys_written DESC
LIMIT 100;
```

```sql
-- For a single tx: compute per-key before/after by taking the first old value and last new value.
-- (If a key is written multiple times in the same tx, intermediate values are omitted here.)
WITH first_write AS (
  SELECT DISTINCT ON (block_index, tx_hash, contract_hash, key_base64)
         block_index, tx_hash, contract_hash, key_base64,
         old_value_base64 AS value_before
  FROM storage_writes
  WHERE tx_hash = '0x...'
  ORDER BY block_index, tx_hash, contract_hash, key_base64, write_order ASC
),
last_write AS (
  SELECT DISTINCT ON (block_index, tx_hash, contract_hash, key_base64)
         block_index, tx_hash, contract_hash, key_base64,
         is_delete,
         new_value_base64 AS value_after
  FROM storage_writes
  WHERE tx_hash = '0x...'
  ORDER BY block_index, tx_hash, contract_hash, key_base64, write_order DESC
)
SELECT f.block_index, f.tx_hash, f.contract_hash, f.key_base64, f.value_before, l.value_after, l.is_delete
FROM first_write f
JOIN last_write l
  USING (block_index, tx_hash, contract_hash, key_base64)
ORDER BY f.contract_hash, f.key_base64;
```

```sql
-- For a single block+contract_id: approximate "value at block start" for keys written in that block.
-- Prefer storage_reads (deduped first-read per key per block) when present; otherwise fall back to the first write's old value.
WITH keys_written AS (
  SELECT block_index, contract_id, key_base64,
         MIN(write_order) AS first_write_order,
         MAX(write_order) AS last_write_order
  FROM storage_writes
  WHERE block_index = 5000000 AND contract_id = 123
  GROUP BY block_index, contract_id, key_base64
),
first_write AS (
  SELECT k.block_index, k.contract_id, k.key_base64,
         w.tx_hash,
         w.old_value_base64
  FROM keys_written k
  JOIN storage_writes w
    ON w.block_index = k.block_index
   AND w.contract_id = k.contract_id
   AND w.key_base64 = k.key_base64
   AND w.write_order = k.first_write_order
),
last_write AS (
  SELECT k.block_index, k.contract_id, k.key_base64,
         w.is_delete,
         w.new_value_base64
  FROM keys_written k
  JOIN storage_writes w
    ON w.block_index = k.block_index
   AND w.contract_id = k.contract_id
   AND w.key_base64 = k.key_base64
   AND w.write_order = k.last_write_order
)
SELECT f.key_base64,
       COALESCE(r.value_base64, f.old_value_base64) AS value_at_block_start,
       l.new_value_base64 AS value_after_last_write,
       l.is_delete,
       f.tx_hash AS first_writer_tx
FROM first_write f
LEFT JOIN storage_reads r
  ON r.block_index = f.block_index
 AND r.contract_id = f.contract_id
 AND r.key_base64 = f.key_base64
JOIN last_write l
  ON l.block_index = f.block_index
 AND l.contract_id = f.contract_id
 AND l.key_base64 = f.key_base64
ORDER BY f.key_base64;
```

Tip: `key_base64` / `*_value_base64` can be decoded in SQL via `decode(key_base64, 'base64')` when you want raw bytes.

## 14.7 Storage reads (initial key/value per block)

`storage_reads` is deduped per `(block_index, contract_id, key_base64)`: it captures the first observed value of each key during block execution (including which tx triggered that first read).

```sql
-- Which tx first observed each key for a contract in a block (ordered by read_order)
SELECT read_order, tx_hash, key_base64, value_base64, source
FROM storage_reads
WHERE block_index = 5000000 AND contract_id = 123
ORDER BY read_order
LIMIT 500;
```
