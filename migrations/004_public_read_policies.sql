-- Migration: 004_public_read_policies.sql
-- Description: Enable RLS + public read policies for base tables and storage bucket.
-- Date: 2025-12-12

-- ============================================
-- Base tables (blocks / contracts / storage_reads)
-- ============================================

ALTER TABLE blocks ENABLE ROW LEVEL SECURITY;
ALTER TABLE contracts ENABLE ROW LEVEL SECURITY;
ALTER TABLE storage_reads ENABLE ROW LEVEL SECURITY;

-- Public SELECT policies (idempotent)
DO $policy$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'blocks' AND polname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON blocks FOR SELECT USING (true)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'contracts' AND polname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON contracts FOR SELECT USING (true)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'storage_reads' AND polname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON storage_reads FOR SELECT USING (true)';
    END IF;
END;
$policy$;

-- ============================================
-- Storage bucket read access
-- ============================================
-- Allows anon users to download binary snapshots from the "block-state" bucket.
-- If you prefer a private bucket, remove this and use signed URLs instead.

ALTER TABLE storage.objects ENABLE ROW LEVEL SECURITY;

DO $policy$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_policies
        WHERE schemaname = 'storage'
          AND tablename = 'objects'
          AND polname = 'Allow public read access to block-state bucket'
    ) THEN
        EXECUTE $sql$
            CREATE POLICY "Allow public read access to block-state bucket"
            ON storage.objects
            FOR SELECT
            USING (bucket_id = 'block-state')
        $sql$;
    END IF;
END;
$policy$;

