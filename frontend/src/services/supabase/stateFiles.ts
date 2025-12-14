import { getSupabase, supabaseBucket } from './client';

export async function downloadStateFile(blockIndex: number, format: 'bin' | 'json' | 'csv'): Promise<Blob> {
  const supabase = getSupabase();
  const fileName = `block-${blockIndex}.${format}`;

  const { data, error } = await supabase.storage.from(supabaseBucket).download(fileName);

  if (error) {
    throw new Error(`Failed to download state file (${format}): ${error.message}`);
  }

  return data;
}

export async function stateFileExists(blockIndex: number, format: 'bin' | 'json' | 'csv'): Promise<boolean> {
  const supabase = getSupabase();
  const fileName = `block-${blockIndex}.${format}`;
  const { data, error } = await supabase.storage.from(supabaseBucket).list('', { search: fileName, limit: 1 });
  if (error) return false;
  return (data ?? []).some((item) => item.name === fileName);
}

