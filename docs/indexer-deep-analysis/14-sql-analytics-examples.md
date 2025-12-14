# 14. SQL analytics examples (results + traces)

The indexer writes a “thin but complete” transaction outcome row to `transaction_results` and (optionally) detailed traces to the partitioned trace tables (`opcode_traces`, `syscall_traces`, `contract_calls`, `storage_writes`, `notifications`).

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
