import type { ContractCallStat, OpCodeStat, SyscallCategory, SyscallStat } from '../../../types';

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

