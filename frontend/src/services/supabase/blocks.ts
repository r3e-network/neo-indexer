import type { Block, BlockSearchFilters, PaginatedResponse } from '../../types';
import { getSupabase } from './client';

export async function searchBlocks(
  filters: BlockSearchFilters,
  page = 1,
  pageSize = 50
): Promise<PaginatedResponse<Block>> {
  const supabase = getSupabase();
  const offset = (page - 1) * pageSize;

  let query = supabase.from('blocks').select('*', { count: 'exact' });

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

export async function getBlockByIndex(blockIndex: number): Promise<Block | null> {
  const supabase = getSupabase();
  const { data, error } = await supabase.from('blocks').select('*').eq('block_index', blockIndex).single();

  if (error) {
    if (error.code === 'PGRST116') return null;
    throw new Error(`Failed to get block: ${error.message}`);
  }

  return data;
}

