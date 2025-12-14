import type { PaginatedResponse, StorageRead } from '../../types';
import { getSupabase } from './client';

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

