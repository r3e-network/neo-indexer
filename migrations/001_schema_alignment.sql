-- Migration: Schema Alignment for Neo Block State Indexer
-- Version: 001
-- Date: 2025-12-11
-- Description: Align Supabase schema with StateRecorderSupabase.cs expectations

-- ============================================================================
-- STEP 0: Ensure blocks table exists (fresh Supabase projects)
-- ============================================================================

CREATE TABLE IF NOT EXISTS blocks (
    block_index INTEGER PRIMARY KEY,
    hash TEXT NOT NULL,
    timestamp_ms BIGINT,
    tx_count INTEGER NOT NULL DEFAULT 0,
    read_key_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================================================
-- STEP 1: Modify existing blocks table to match code expectations
-- ============================================================================

-- Add block_index column if it doesn't exist (maps from existing 'index' column)
ALTER TABLE blocks ADD COLUMN IF NOT EXISTS block_index INTEGER;

-- Ensure required metadata columns exist
ALTER TABLE blocks ADD COLUMN IF NOT EXISTS hash TEXT;
ALTER TABLE blocks ADD COLUMN IF NOT EXISTS tx_count INTEGER DEFAULT 0;

-- Add read_key_count column
ALTER TABLE blocks ADD COLUMN IF NOT EXISTS read_key_count INTEGER DEFAULT 0;

-- Add timestamp_ms column (milliseconds)
ALTER TABLE blocks ADD COLUMN IF NOT EXISTS timestamp_ms BIGINT;

-- Add created_at and updated_at timestamps
ALTER TABLE blocks ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ DEFAULT NOW();
ALTER TABLE blocks ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();

-- Migrate legacy data if prior schema used different columns.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'blocks' AND column_name = 'index'
    ) THEN
        UPDATE blocks SET block_index = "index" WHERE block_index IS NULL;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'blocks' AND column_name = 'timestamp'
    ) THEN
        UPDATE blocks
        SET timestamp_ms = timestamp * 1000
        WHERE timestamp_ms IS NULL AND timestamp IS NOT NULL;
    END IF;
END;
$$;

-- Ensure block_index has a unique constraint for ON CONFLICT upserts.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey)
        WHERE c.conrelid = 'public.blocks'::regclass
          AND c.contype IN ('p', 'u')
          AND a.attname = 'block_index'
    ) THEN
        ALTER TABLE blocks ADD CONSTRAINT blocks_block_index_unique UNIQUE (block_index);
    END IF;
END;
$$;

-- Create index on timestamp_ms for time-range queries
CREATE INDEX IF NOT EXISTS idx_blocks_timestamp_ms ON blocks(timestamp_ms);

-- ============================================================================
-- STEP 2: Create contracts table
-- ============================================================================

CREATE TABLE IF NOT EXISTS contracts (
    contract_id INTEGER PRIMARY KEY,
    contract_hash TEXT NOT NULL,
    manifest_name TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create index on contract_hash for lookups
CREATE INDEX IF NOT EXISTS idx_contracts_hash ON contracts(contract_hash);

-- ============================================================================
-- STEP 3: Create storage_reads table
-- ============================================================================

CREATE TABLE IF NOT EXISTS storage_reads (
    id BIGSERIAL PRIMARY KEY,
    block_index INTEGER NOT NULL,
    contract_id INTEGER,
    key_base64 TEXT NOT NULL,
    value_base64 TEXT NOT NULL,
    read_order INTEGER NOT NULL,
    tx_hash TEXT,
    source TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create indexes for efficient queries and deletes
CREATE INDEX IF NOT EXISTS idx_storage_reads_block_index ON storage_reads(block_index);
CREATE INDEX IF NOT EXISTS idx_storage_reads_contract_id ON storage_reads(contract_id);
CREATE INDEX IF NOT EXISTS idx_storage_reads_block_contract ON storage_reads(block_index, contract_id);
CREATE INDEX IF NOT EXISTS idx_storage_reads_tx_hash ON storage_reads(tx_hash);
CREATE INDEX IF NOT EXISTS idx_storage_reads_block_order ON storage_reads(block_index, read_order);

-- Foreign key to enable PostgREST relationship embedding.
-- Marked NOT VALID to avoid long scans on already-populated databases.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE table_schema = 'public'
          AND table_name = 'storage_reads'
          AND constraint_name = 'storage_reads_contract_id_fkey'
    ) THEN
        ALTER TABLE storage_reads
            ADD CONSTRAINT storage_reads_contract_id_fkey
            FOREIGN KEY (contract_id) REFERENCES contracts(contract_id)
            ON DELETE SET NULL
            NOT VALID;
    END IF;
END;
$$;

-- ============================================================================
-- STEP 4: Create storage bucket for binary uploads (run in Supabase Dashboard)
-- ============================================================================
-- Note: Execute this in Supabase Dashboard -> Storage -> New Bucket
-- Bucket name: block-state
-- Public: false
-- File size limit: 50MB

-- ============================================================================
-- VERIFICATION QUERIES
-- ============================================================================

-- Verify blocks table structure
-- SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'blocks';

-- Verify contracts table exists
-- SELECT COUNT(*) FROM contracts;

-- Verify storage_reads table exists
-- SELECT COUNT(*) FROM storage_reads;

-- Verify indexes exist
-- SELECT indexname FROM pg_indexes WHERE tablename IN ('blocks', 'contracts', 'storage_reads');
