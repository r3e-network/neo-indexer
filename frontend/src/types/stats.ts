export type SyscallCategory = 'storage' | 'contract' | 'runtime' | 'system' | 'crypto' | 'network' | 'other';

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

