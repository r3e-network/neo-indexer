import type {
  ContractCallStat,
  ContractCallTraceEntry,
  OpCodeStat,
  OpCodeTraceEntry,
  SyscallCategory,
  SyscallStat,
  SyscallTraceEntry,
} from '../../types';

export interface RawOpCodeTrace {
  block_index?: number;
  tx_hash?: string;
  contract_hash: string;
  instruction_pointer?: number;
  opcode?: string | number;
  opcode_name?: string;
  operand_base64?: string | null;
  gas_consumed?: number;
  stack_depth?: number | null;
  trace_order?: number;
}

export interface RawSyscallTrace {
  block_index?: number;
  tx_hash?: string;
  contract_hash: string;
  syscall_name: string;
  syscall_hash?: string;
  gas_cost?: number;
  trace_order?: number;
}

export interface RawContractCallTrace {
  block_index?: number;
  tx_hash?: string;
  caller_hash?: string | null;
  callee_hash: string;
  method_name?: string | null;
  call_depth?: number;
  trace_order?: number;
  success?: boolean;
  gas_consumed?: number | null;
}

export interface RawSyscallStat {
  syscall_hash?: string;
  syscall_name: string;
  call_count: number;
  total_gas_cost?: number | null;
  avg_gas_cost?: number | null;
  min_gas_cost?: number | null;
  max_gas_cost?: number | null;
  first_block?: number | null;
  last_block?: number | null;
  gas_base?: number | null;
  category?: string | null;
}

export interface RawOpCodeStat {
  opcode: number;
  opcode_name: string;
  call_count: number;
  total_gas_consumed?: number;
  avg_gas_consumed?: number;
  min_gas_consumed?: number;
  max_gas_consumed?: number;
  first_block?: number;
  last_block?: number;
}

export interface RawContractCallStat {
  callee_hash: string;
  caller_hash?: string | null;
  method_name?: string | null;
  call_count: number;
  success_count?: number | null;
  failure_count?: number | null;
  total_gas_consumed?: number | null;
  avg_gas_consumed?: number | null;
  first_block?: number | null;
  last_block?: number | null;
}

export function normalizeOpCodeTrace(
  raw: RawOpCodeTrace,
  fallbackTxHash: string,
  fallbackBlockIndex: number
): OpCodeTraceEntry {
  const opcodeName =
    typeof raw.opcode === 'string'
      ? raw.opcode
      : raw.opcode_name ?? (typeof raw.opcode === 'number' ? `0x${raw.opcode.toString(16)}` : 'UNKNOWN');

  return {
    blockIndex: raw.block_index ?? fallbackBlockIndex,
    txHash: raw.tx_hash ?? fallbackTxHash,
    contractHash: raw.contract_hash,
    instructionPointer: raw.instruction_pointer ?? 0,
    opcode: typeof raw.opcode === 'string' ? raw.opcode : opcodeName,
    opcodeName,
    operand: raw.operand_base64 ?? null,
    gasConsumed: raw.gas_consumed ?? 0,
    stackDepth: raw.stack_depth ?? 0,
    order: raw.trace_order ?? 0,
  };
}

export function normalizeSyscallTrace(
  raw: RawSyscallTrace,
  fallbackTxHash: string,
  fallbackBlockIndex: number
): SyscallTraceEntry {
  return {
    blockIndex: raw.block_index ?? fallbackBlockIndex,
    txHash: raw.tx_hash ?? fallbackTxHash,
    contractHash: raw.contract_hash,
    syscallName: raw.syscall_name,
    syscallHash: raw.syscall_hash,
    gasCost: raw.gas_cost ?? 0,
    order: raw.trace_order ?? 0,
  };
}

export function normalizeContractCallTrace(
  raw: RawContractCallTrace,
  fallbackTxHash: string,
  fallbackBlockIndex: number
): ContractCallTraceEntry {
  return {
    blockIndex: raw.block_index ?? fallbackBlockIndex,
    txHash: raw.tx_hash ?? fallbackTxHash,
    callerHash: raw.caller_hash ?? null,
    calleeHash: raw.callee_hash,
    methodName: raw.method_name,
    callDepth: raw.call_depth ?? 0,
    order: raw.trace_order ?? 0,
    success: raw.success ?? true,
    gasConsumed: raw.gas_consumed ?? 0,
  };
}

function normalizeSyscallCategory(rawCategory?: string): SyscallCategory {
  const normalized = rawCategory?.toLowerCase() ?? '';
  if (normalized.includes('storage')) return 'storage';
  if (normalized.includes('contract')) return 'contract';
  if (normalized.includes('runtime') || normalized.includes('vm')) return 'runtime';
  if (normalized.includes('policy') || normalized.includes('system')) return 'system';
  if (normalized.includes('crypto')) return 'crypto';
  if (normalized.includes('oracle') || normalized.includes('network')) return 'network';
  return 'other';
}

export function normalizeSyscallStat(raw: RawSyscallStat): SyscallStat {
  const categoryHint = raw.category ?? raw.syscall_name;
  return {
    syscallName: raw.syscall_name,
    callCount: raw.call_count,
    totalGas: raw.total_gas_cost ?? 0,
    category: normalizeSyscallCategory(categoryHint),
  };
}

export function normalizeOpCodeStat(raw: RawOpCodeStat): OpCodeStat {
  return {
    opcode: raw.opcode,
    opcodeName: raw.opcode_name,
    callCount: raw.call_count,
    totalGasConsumed: raw.total_gas_consumed ?? 0,
    averageGasConsumed: raw.avg_gas_consumed ?? undefined,
    minGasConsumed: raw.min_gas_consumed ?? undefined,
    maxGasConsumed: raw.max_gas_consumed ?? undefined,
    firstBlock: raw.first_block ?? undefined,
    lastBlock: raw.last_block ?? undefined,
  };
}

export function normalizeContractCallStat(raw: RawContractCallStat): ContractCallStat {
  return {
    calleeHash: raw.callee_hash,
    callerHash: raw.caller_hash ?? null,
    methodName: raw.method_name ?? null,
    callCount: raw.call_count,
    successCount: raw.success_count ?? 0,
    failureCount: raw.failure_count ?? 0,
    totalGasConsumed: raw.total_gas_consumed ?? 0,
    averageGasConsumed: raw.avg_gas_consumed ?? null,
    firstBlock: raw.first_block ?? null,
    lastBlock: raw.last_block ?? null,
  };
}

