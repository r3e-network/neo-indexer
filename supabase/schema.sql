-- Partitioned opcode trace store for Neo N3 mainnet
-- Run in Supabase SQL Editor (PostgreSQL 15+)

-- 1. Blocks and transactions
CREATE TABLE IF NOT EXISTS blocks (
    index INT PRIMARY KEY,
    hash CHAR(66) NOT NULL,
    timestamp BIGINT NOT NULL,
    tx_count INT DEFAULT 0
);

CREATE TABLE IF NOT EXISTS transactions (
    hash CHAR(66) PRIMARY KEY,
    block_index INT NOT NULL,
    sender CHAR(42),
    sys_fee BIGINT,
    net_fee BIGINT
);
CREATE INDEX IF NOT EXISTS idx_tx_block ON transactions(block_index);

-- 2. Opcode trace partitioned table
CREATE TABLE IF NOT EXISTS op_traces (
    tx_hash CHAR(66) NOT NULL,
    block_index INT NOT NULL,
    step_order INT NOT NULL,
    contract_hash CHAR(42),
    opcode VARCHAR(32) NOT NULL,
    syscall VARCHAR(100),
    gas_consumed BIGINT,
    stack_top TEXT,
    PRIMARY KEY (block_index, tx_hash, step_order)
) PARTITION BY RANGE (block_index);

-- Indexes cascade to partitions
CREATE INDEX IF NOT EXISTS idx_trace_tx ON op_traces(tx_hash);
CREATE INDEX IF NOT EXISTS idx_trace_contract ON op_traces(contract_hash);
CREATE INDEX IF NOT EXISTS idx_trace_opcode ON op_traces(opcode);

-- 3. Partition helpers
CREATE OR REPLACE FUNCTION create_partition(start_idx INT, end_idx INT)
RETURNS void AS $$
DECLARE
    table_name TEXT := 'op_traces_p' || start_idx || '_' || end_idx;
BEGIN
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF op_traces FOR VALUES FROM (%L) TO (%L)',
        table_name, start_idx, end_idx
    );
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION drop_partition(start_idx INT, end_idx INT)
RETURNS void AS $$
DECLARE
    table_name TEXT := 'op_traces_p' || start_idx || '_' || end_idx;
BEGIN
    EXECUTE format('DROP TABLE IF EXISTS %I', table_name);
END;
$$ LANGUAGE plpgsql;

-- 4. Pre-provision initial partitions (extend as chain grows)
SELECT create_partition(0, 100000);
SELECT create_partition(100000, 200000);
SELECT create_partition(200000, 300000);
SELECT create_partition(300000, 400000);
SELECT create_partition(400000, 500000);
