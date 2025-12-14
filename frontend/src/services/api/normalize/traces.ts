import type { ContractCallTraceEntry, OpCodeTraceEntry, SyscallTraceEntry } from '../../../types';

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

