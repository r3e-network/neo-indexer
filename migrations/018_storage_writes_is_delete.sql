-- Migration: 018_storage_writes_is_delete.sql
-- Description: Disambiguate storage deletes from empty-value writes.
-- Date: 2025-12-15
--
-- storage_writes previously represented deletes by writing an empty new_value_base64, which
-- is ambiguous because contracts can legally store empty byte arrays. This migration adds
-- an explicit boolean flag to preserve intent.

ALTER TABLE storage_writes
    ADD COLUMN IF NOT EXISTS is_delete BOOLEAN NOT NULL DEFAULT false;

COMMENT ON COLUMN storage_writes.is_delete IS
    'True when the storage write represents a delete operation (as opposed to put/update).';

