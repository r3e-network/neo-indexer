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

/** Search filters for block queries */
export interface BlockSearchFilters {
  blockIndex?: number;
  hash?: string;
  startDate?: Date;
  endDate?: Date;
}

/** Paginated response */
export interface PaginatedResponse<T> {
  data: T[];
  count: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
}

/** Export format options */
export type ExportFormat = "csv" | "json" | "binary";

/** Normalized opcode trace entry returned by trace APIs */
export interface OpCodeTraceEntry {
  blockIndex: number;
  txHash: string;
  contractHash: string;
  instructionPointer: number;
  opcode: string;
  opcodeName: string;
  operand?: string | null;
  gasConsumed: number;
  stackDepth: number;
  order: number;
}

/** Syscall trace entry */
export interface SyscallTraceEntry {
  blockIndex: number;
  txHash: string;
  contractHash: string;
  syscallName: string;
  syscallHash?: string;
  gasCost: number;
  order: number;
}

/** Contract call trace entry */
export interface ContractCallTraceEntry {
  blockIndex: number;
  txHash: string;
  callerHash: string | null;
  calleeHash: string;
  methodName?: string | null;
  callDepth: number;
  order: number;
  success?: boolean;
  gasConsumed?: number;
}

/** Transaction-level trace response */
export interface TransactionTraceResult {
  txHash: string;
  blockIndex: number;
  opcodes: OpCodeTraceEntry[];
  syscalls: SyscallTraceEntry[];
  contractCalls: ContractCallTraceEntry[];
}

/** Block-level trace response */
export interface BlockTraceResult {
  blockIndex: number;
  blockHash: string;
  transactions: TransactionTraceResult[];
}

/** Contract call graph payload */
export interface ContractCallGraph {
  contractHash?: string;
  calls: ContractCallTraceEntry[];
}

export type SyscallCategory = "storage" | "contract" | "runtime" | "system" | "crypto" | "network" | "other";

/** Aggregated syscall statistics */
export interface SyscallStat {
  syscallName: string;
  callCount: number;
  totalGas: number;
  category: SyscallCategory;
}

/** Aggregated opcode statistics */
export interface OpCodeStat {
  opcode: number;
  opcodeName: string;
  callCount: number;
  totalGasConsumed: number;
  averageGasConsumed?: number;
  minGasConsumed?: number;
  maxGasConsumed?: number;
  firstBlock?: number;
  lastBlock?: number;
}

/** Aggregated contract/native method statistics */
export interface ContractCallStat {
  calleeHash: string;
  callerHash: string | null;
  methodName: string | null;
  callCount: number;
  successCount: number;
  failureCount: number;
  totalGasConsumed: number;
  averageGasConsumed?: number | null;
  firstBlock?: number | null;
  lastBlock?: number | null;
}
