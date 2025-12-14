/** Block record from Supabase */
export interface Block {
  block_index: number;
  hash: string;
  timestamp_ms: number;
  tx_count: number;
  read_key_count: number;
  created_at?: string;
}

/** Contract record from Supabase */
export interface Contract {
  contract_id: number;
  contract_hash: string;
  manifest_name: string | null;
  created_at?: string;
}

/** Storage read record from Supabase */
export interface StorageRead {
  id: number;
  block_index: number;
  contract_id: number | null;
  key_base64: string;
  value_base64: string;
  read_order: number;
  tx_hash: string | null;
  source: string | null;
  created_at?: string;
  // Joined fields
  contracts?: Contract;
}

