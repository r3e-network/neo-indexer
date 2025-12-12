import { createClient, SupabaseClient } from '@supabase/supabase-js';
import type { Block, StorageRead, PaginatedResponse, BlockSearchFilters } from '../types';

const supabaseUrl = import.meta.env.VITE_SUPABASE_URL;
const supabaseKey = import.meta.env.VITE_SUPABASE_ANON_KEY;
const supabaseBucket = import.meta.env.VITE_SUPABASE_BUCKET || 'block-state';

let supabaseInstance: SupabaseClient | null = null;

/**
 * Get or create Supabase client instance
 */
export function getSupabase(): SupabaseClient {
  if (!supabaseInstance) {
    if (!supabaseUrl || !supabaseKey) {
      throw new Error('Missing Supabase configuration. Set VITE_SUPABASE_URL and VITE_SUPABASE_ANON_KEY.');
    }
    supabaseInstance = createClient(supabaseUrl, supabaseKey);
  }
  return supabaseInstance;
}

/**
 * Search blocks with filters and pagination
 */
export async function searchBlocks(
  filters: BlockSearchFilters,
  page = 1,
  pageSize = 50
): Promise<PaginatedResponse<Block>> {
  const supabase = getSupabase();
  const offset = (page - 1) * pageSize;

  let query = supabase.from('blocks').select('*', { count: 'exact' });

  // Apply filters
  if (filters.blockIndex !== undefined) {
    query = query.eq('block_index', filters.blockIndex);
  }
  if (filters.hash) {
    query = query.eq('hash', filters.hash);
  }
  if (filters.startDate) {
    query = query.gte('timestamp_ms', filters.startDate.getTime());
  }
  if (filters.endDate) {
    query = query.lte('timestamp_ms', filters.endDate.getTime());
  }

  // Apply pagination
  query = query.order('block_index', { ascending: false }).range(offset, offset + pageSize - 1);

  const { data, count, error } = await query;

  if (error) {
    throw new Error(`Failed to search blocks: ${error.message}`);
  }

  return {
    data: data ?? [],
    count: count ?? 0,
    page,
    pageSize,
    hasMore: (count ?? 0) > offset + pageSize,
  };
}

/**
 * Get block by index
 */
export async function getBlockByIndex(blockIndex: number): Promise<Block | null> {
  const supabase = getSupabase();
  const { data, error } = await supabase.from('blocks').select('*').eq('block_index', blockIndex).single();

  if (error) {
    if (error.code === 'PGRST116') return null; // Not found
    throw new Error(`Failed to get block: ${error.message}`);
  }

  return data;
}

/**
 * Get storage reads for a block with pagination
 */
export async function getStorageReads(
  blockIndex: number,
  page = 1,
  pageSize = 50
): Promise<PaginatedResponse<StorageRead>> {
  const supabase = getSupabase();
  const offset = (page - 1) * pageSize;

  const { data, count, error } = await supabase
    .from('storage_reads')
    .select('*, contracts(contract_hash, manifest_name)', { count: 'exact' })
    .eq('block_index', blockIndex)
    .order('read_order', { ascending: true })
    .range(offset, offset + pageSize - 1);

  if (error) {
    throw new Error(`Failed to get storage reads: ${error.message}`);
  }

  return {
    data: data ?? [],
    count: count ?? 0,
    page,
    pageSize,
    hasMore: (count ?? 0) > offset + pageSize,
  };
}

/**
 * Get all storage reads for a block (for export)
 */
export async function getAllStorageReads(blockIndex: number): Promise<StorageRead[]> {
  const supabase = getSupabase();
  const allReads: StorageRead[] = [];
  const batchSize = 1000;
  let offset = 0;
  let hasMore = true;

  while (hasMore) {
    const { data, error } = await supabase
      .from('storage_reads')
      .select('*, contracts(contract_hash, manifest_name)')
      .eq('block_index', blockIndex)
      .order('read_order', { ascending: true })
      .range(offset, offset + batchSize - 1);

    if (error) {
      throw new Error(`Failed to get storage reads: ${error.message}`);
    }

    if (data && data.length > 0) {
      allReads.push(...data);
      offset += batchSize;
      hasMore = data.length === batchSize;
    } else {
      hasMore = false;
    }
  }

  return allReads;
}

/**
 * Download state file from Supabase Storage
 */
export async function downloadStateFile(blockIndex: number, format: 'bin' | 'json' | 'csv'): Promise<Blob> {
  const supabase = getSupabase();
  const fileName = `block-${blockIndex}.${format}`;

  const { data, error } = await supabase.storage.from(supabaseBucket).download(fileName);

  if (error) {
    throw new Error(`Failed to download state file (${format}): ${error.message}`);
  }

  return data;
}

/**
 * Check if a state file exists in storage (exact object). Uses HEAD via download to avoid substring matches.
 */
export async function stateFileExists(blockIndex: number, format: 'bin' | 'json' | 'csv'): Promise<boolean> {
  const supabase = getSupabase();
  const fileName = `block-${blockIndex}.${format}`;
  const { data, error } = await supabase.storage.from(supabaseBucket).download(fileName);
  if (error) return false;
  if (data) return true;
  return false;
}
