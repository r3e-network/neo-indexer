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

