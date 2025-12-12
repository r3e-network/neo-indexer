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
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'blocks' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON blocks FOR SELECT USING (true)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'contracts' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON contracts FOR SELECT USING (true)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname = 'public' AND tablename = 'storage_reads' AND policyname = 'Allow public read access') THEN
        EXECUTE 'CREATE POLICY "Allow public read access" ON storage_reads FOR SELECT USING (true)';
    END IF;
END;
$policy$;

-- Service role write policies (idempotent).
-- The indexer uses the Supabase service role key; frontend is read-only.
DO $policy$
BEGIN
    -- Drop any legacy wide-open policies.
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON blocks';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON contracts';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated insert" ON storage_reads';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON blocks';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON contracts';
    EXECUTE 'DROP POLICY IF EXISTS "Allow authenticated upsert" ON storage_reads';

    -- Drop prior service role policies if re-running.
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON blocks';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON contracts';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role insert" ON storage_reads';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON blocks';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON contracts';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role upsert" ON storage_reads';
    EXECUTE 'DROP POLICY IF EXISTS "Allow service role delete" ON storage_reads';

    -- Only allow writes when JWT role is service_role.
    EXECUTE 'CREATE POLICY "Allow service role insert" ON blocks FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role upsert" ON blocks FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';

    EXECUTE 'CREATE POLICY "Allow service role insert" ON contracts FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role upsert" ON contracts FOR UPDATE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'') WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';

    EXECUTE 'CREATE POLICY "Allow service role delete" ON storage_reads FOR DELETE USING (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
    EXECUTE 'CREATE POLICY "Allow service role insert" ON storage_reads FOR INSERT WITH CHECK (current_setting(''request.jwt.claim.role'', true) = ''service_role'')';
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
          AND policyname = 'Allow public read access to block-state bucket'
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
