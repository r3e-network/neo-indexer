import type {
  BlockTraceResult,
  ContractCallGraph,
  ContractCallTraceEntry,
  OpCodeTraceEntry,
  OpCodeStat,
  SyscallCategory,
  SyscallStat,
  SyscallTraceEntry,
  TransactionTraceResult,
} from '../types';
import { getSupabase } from './supabase';

interface RawOpCodeTrace {
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

interface RawSyscallTrace {
  block_index?: number;
  tx_hash?: string;
  contract_hash: string;
  syscall_name: string;
  syscall_hash?: string;
  gas_cost?: number;
  trace_order?: number;
}

interface RawContractCallTrace {
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

interface RawSyscallStat {
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

interface RawOpCodeStat {
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

function normalizeOpCodeTrace(raw: RawOpCodeTrace, fallbackTxHash: string, fallbackBlockIndex: number): OpCodeTraceEntry {
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

function normalizeSyscallTrace(
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

function normalizeContractCallTrace(
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

function normalizeSyscallStat(raw: RawSyscallStat): SyscallStat {
  const categoryHint = raw.category ?? raw.syscall_name;
  return {
    syscallName: raw.syscall_name,
    callCount: raw.call_count,
    totalGas: raw.total_gas_cost ?? 0,
    category: normalizeSyscallCategory(categoryHint),
  };
}

function normalizeOpCodeStat(raw: RawOpCodeStat): OpCodeStat {
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

async function fetchAllTraces<T>(table: string, blockIndex: number): Promise<T[]> {
  const supabase = getSupabase();
  const results: T[] = [];
  const batchSize = 5000;
  let offset = 0;

  while (true) {
    const { data, error } = await supabase
      .from(table)
      .select('*')
      .eq('block_index', blockIndex)
      .order('trace_order', { ascending: true })
      .range(offset, offset + batchSize - 1);

    if (error) {
      throw new Error(`Failed to fetch ${table}: ${error.message}`);
    }

    if (!data || data.length === 0) break;
    results.push(...(data as unknown as T[]));
    if (data.length < batchSize) break;
    offset += batchSize;
  }

  return results;
}

async function fetchAllByTx<T>(table: string, txHash: string): Promise<T[]> {
  const supabase = getSupabase();
  const results: T[] = [];
  const batchSize = 5000;
  let offset = 0;

  while (true) {
    const { data, error } = await supabase
      .from(table)
      .select('*')
      .eq('tx_hash', txHash)
      .order('trace_order', { ascending: true })
      .range(offset, offset + batchSize - 1);

    if (error) {
      throw new Error(`Failed to fetch ${table}: ${error.message}`);
    }

    if (!data || data.length === 0) break;
    results.push(...(data as unknown as T[]));
    if (data.length < batchSize) break;
    offset += batchSize;
  }

  return results;
}

export async function fetchBlockTrace(blockIndex: number): Promise<BlockTraceResult> {
  const supabase = getSupabase();

  const [{ data: blockRow, error: blockError }, opcodes, syscalls, calls] = await Promise.all([
    supabase.from('blocks').select('hash').eq('block_index', blockIndex).maybeSingle(),
    fetchAllTraces<RawOpCodeTrace>('opcode_traces', blockIndex),
    fetchAllTraces<RawSyscallTrace>('syscall_traces', blockIndex),
    fetchAllTraces<RawContractCallTrace>('contract_calls', blockIndex),
  ]);

  if (blockError) {
    throw new Error(`Failed to fetch block metadata: ${blockError.message}`);
  }

  const txHashes = new Set<string>();
  opcodes.forEach((row) => row.tx_hash && txHashes.add(row.tx_hash));
  syscalls.forEach((row) => row.tx_hash && txHashes.add(row.tx_hash));
  calls.forEach((row) => row.tx_hash && txHashes.add(row.tx_hash));

  const transactions: TransactionTraceResult[] = Array.from(txHashes)
    .sort()
    .map((txHash) => ({
      txHash,
      blockIndex,
      opcodes: opcodes.filter((row) => row.tx_hash === txHash).map((row) => normalizeOpCodeTrace(row, txHash, blockIndex)),
      syscalls: syscalls.filter((row) => row.tx_hash === txHash).map((row) => normalizeSyscallTrace(row, txHash, blockIndex)),
      contractCalls: calls
        .filter((row) => row.tx_hash === txHash)
        .map((row) => normalizeContractCallTrace(row, txHash, blockIndex)),
    }));

  return {
    blockIndex,
    blockHash: blockRow?.hash ?? '',
    transactions,
  };
}

export async function fetchTransactionTrace(txHash: string): Promise<TransactionTraceResult> {
  const [opcodes, syscalls, calls] = await Promise.all([
    fetchAllByTx<RawOpCodeTrace>('opcode_traces', txHash),
    fetchAllByTx<RawSyscallTrace>('syscall_traces', txHash),
    fetchAllByTx<RawContractCallTrace>('contract_calls', txHash),
  ]);

  const blockIndex =
    opcodes[0]?.block_index ??
    syscalls[0]?.block_index ??
    calls[0]?.block_index ??
    null;

  if (blockIndex === null || blockIndex === undefined) {
    throw new Error(`No traces found for transaction ${txHash}`);
  }

  return {
    txHash,
    blockIndex,
    opcodes: opcodes.map((row) => normalizeOpCodeTrace(row, txHash, blockIndex)),
    syscalls: syscalls.map((row) => normalizeSyscallTrace(row, txHash, blockIndex)),
    contractCalls: calls.map((row) => normalizeContractCallTrace(row, txHash, blockIndex)),
  };
}

export async function fetchContractCalls(contractHash: string): Promise<ContractCallGraph> {
  const supabase = getSupabase();
  const { data, error } = await supabase
    .from('contract_calls')
    .select('*')
    .or(`caller_hash.eq.${contractHash},callee_hash.eq.${contractHash}`)
    .order('block_index', { ascending: false })
    .limit(1000);

  if (error) {
    throw new Error(`Failed to fetch contract calls: ${error.message}`);
  }

  const calls = (data ?? []).map((row) =>
    normalizeContractCallTrace(row as RawContractCallTrace, row.tx_hash ?? contractHash, row.block_index ?? 0)
  );

  return {
    contractHash,
    calls,
  };
}

export async function fetchSyscallStats(startBlock: number, endBlock: number): Promise<SyscallStat[]> {
  const supabase = getSupabase();
  const { data, error } = await supabase.rpc<RawSyscallStat>('get_syscall_stats', {
    start_block: startBlock,
    end_block: endBlock,
  });

  if (error) {
    throw new Error(`Failed to fetch syscall stats: ${error.message}`);
  }

  return (data ?? []).map((entry) => normalizeSyscallStat(entry));
}

export async function fetchOpCodeStats(startBlock: number, endBlock: number): Promise<OpCodeStat[]> {
  const supabase = getSupabase();
  const { data, error } = await supabase.rpc<RawOpCodeStat>('get_opcode_stats', {
    start_block: startBlock,
    end_block: endBlock,
  });

  if (error) {
    throw new Error(`Failed to fetch opcode stats: ${error.message}`);
  }

  return (data ?? []).map((entry) => normalizeOpCodeStat(entry));
}
