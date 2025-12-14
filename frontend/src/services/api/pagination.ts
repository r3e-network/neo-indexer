import { getSupabase } from '../supabase';

async function fetchAllPaged<T>(fetchPage: (offset: number, batchSize: number) => Promise<T[]>): Promise<T[]> {
  const results: T[] = [];
  const batchSize = 5000;
  let offset = 0;

  while (true) {
    const page = await fetchPage(offset, batchSize);
    if (page.length === 0) break;
    results.push(...page);
    if (page.length < batchSize) break;
    offset += batchSize;
  }

  return results;
}

export async function fetchAllTracesByBlock<T>(table: string, blockIndex: number): Promise<T[]> {
  const supabase = getSupabase();
  return fetchAllPaged<T>(async (offset, batchSize) => {
    const { data, error } = await supabase
      .from(table)
      .select('*')
      .eq('block_index', blockIndex)
      .order('trace_order', { ascending: true })
      .range(offset, offset + batchSize - 1);

    if (error) {
      throw new Error(`Failed to fetch ${table}: ${error.message}`);
    }

    return (data as unknown as T[] | null) ?? [];
  });
}

export async function fetchAllTracesByTx<T>(table: string, txHash: string): Promise<T[]> {
  const supabase = getSupabase();
  return fetchAllPaged<T>(async (offset, batchSize) => {
    const { data, error } = await supabase
      .from(table)
      .select('*')
      .eq('tx_hash', txHash)
      .order('trace_order', { ascending: true })
      .range(offset, offset + batchSize - 1);

    if (error) {
      throw new Error(`Failed to fetch ${table}: ${error.message}`);
    }

    return (data as unknown as T[] | null) ?? [];
  });
}

