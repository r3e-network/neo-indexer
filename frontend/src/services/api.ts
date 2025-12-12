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

type JsonRpcSuccess<T> = {
  jsonrpc: '2.0';
  id: number;
  result: T;
};

type JsonRpcErrorResponse = {
  jsonrpc: '2.0';
  id: number;
  error: {
    code: number;
    message: string;
    data?: unknown;
  };
};

type JsonRpcResponse<T> = JsonRpcSuccess<T> | JsonRpcErrorResponse;

interface RawOpCodeTrace {
  block_index?: number;
  tx_hash?: string;
  contract_hash: string;
  instruction_pointer?: number;
  opcode?: string | number;
  opcode_name?: string;
  operand?: string | null;
  operand_base64?: string | null;
  gas_consumed?: number;
  stack_depth?: number;
  order?: number;
  trace_order?: number;
}

interface RawSyscallTrace {
  block_index?: number;
  tx_hash?: string;
  contract_hash: string;
  syscall_name: string;
  syscall_hash?: string;
  gas_cost?: number;
  order?: number;
  trace_order?: number;
}

interface RawContractCallTrace {
  block_index?: number;
  tx_hash?: string;
  caller_hash?: string | null;
  callee_hash: string;
  method_name?: string | null;
  call_depth?: number;
  order?: number;
  success?: boolean;
  gas_consumed?: number;
}

interface RawTransactionTrace {
  tx_hash: string;
  block_index: number;
  opcodes?: RawOpCodeTrace[];
  syscalls?: RawSyscallTrace[];
  contract_calls?: RawContractCallTrace[];
}

interface RawBlockTrace {
  block_index: number;
  block_hash: string;
  transactions?: RawTransactionTrace[];
}

interface RawContractCallGraph {
  contract_hash?: string;
  calls?: RawContractCallTrace[];
}

interface RawSyscallStat {
  syscall_name: string;
  call_count: number;
  total_gas: number;
  category?: string;
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

interface RawStatsResponse<T> {
  stats?: T[];
}

function getTraceEnv() {
  if (typeof import.meta !== 'undefined' && import.meta.env) {
    return import.meta.env as Record<string, string | undefined>;
  }
  if (typeof globalThis !== 'undefined' && (globalThis as Record<string, any>).__vitest_env__) {
    return (globalThis as Record<string, any>).__vitest_env__ as Record<string, string | undefined>;
  }
  return {};
}

const runtimeEnv = getTraceEnv();
const TRACE_RPC_URL =
  runtimeEnv.VITE_TRACE_RPC_URL ?? (typeof process !== 'undefined' ? process.env?.VITE_TRACE_RPC_URL : undefined);
const TRACE_API_KEY =
  runtimeEnv.VITE_TRACE_API_KEY ?? (typeof process !== 'undefined' ? process.env?.VITE_TRACE_API_KEY : undefined);

function isErrorResponse<T>(response: JsonRpcResponse<T>): response is JsonRpcErrorResponse {
  return (response as JsonRpcErrorResponse).error !== undefined;
}

async function callTraceRpc<T>(method: string, params: unknown[]): Promise<T> {
  if (!TRACE_RPC_URL) {
    throw new Error('Trace RPC URL is not configured. Set VITE_TRACE_RPC_URL.');
  }

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
  };

  if (TRACE_API_KEY) {
    headers['x-api-key'] = TRACE_API_KEY;
  }

  const body = JSON.stringify({
    jsonrpc: '2.0',
    id: Date.now(),
    method,
    params,
  });

  const response = await fetch(TRACE_RPC_URL, {
    method: 'POST',
    headers,
    body,
  });

  if (!response.ok) {
    throw new Error(`Trace RPC request failed with status ${response.status}`);
  }

  const payload = (await response.json()) as JsonRpcResponse<T>;
  if (isErrorResponse(payload)) {
    throw new Error(payload.error?.message ?? 'Trace RPC request failed');
  }
  return payload.result;
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
    operand: raw.operand ?? raw.operand_base64 ?? null,
    gasConsumed: raw.gas_consumed ?? 0,
    stackDepth: raw.stack_depth ?? 0,
    order: raw.order ?? raw.trace_order ?? 0,
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
    order: raw.order ?? raw.trace_order ?? 0,
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
    order: raw.order ?? 0,
    success: raw.success ?? true,
    gasConsumed: raw.gas_consumed ?? 0,
  };
}

function normalizeTransactionTrace(raw: RawTransactionTrace): TransactionTraceResult {
  return {
    txHash: raw.tx_hash,
    blockIndex: raw.block_index,
    opcodes: (raw.opcodes ?? []).map((entry) => normalizeOpCodeTrace(entry, raw.tx_hash, raw.block_index)),
    syscalls: (raw.syscalls ?? []).map((entry) => normalizeSyscallTrace(entry, raw.tx_hash, raw.block_index)),
    contractCalls: (raw.contract_calls ?? []).map((entry) =>
      normalizeContractCallTrace(entry, raw.tx_hash, raw.block_index)
    ),
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
  return {
    syscallName: raw.syscall_name,
    callCount: raw.call_count,
    totalGas: raw.total_gas,
    category: normalizeSyscallCategory(raw.category),
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

export async function fetchBlockTrace(blockIndex: number): Promise<BlockTraceResult> {
  const result = await callTraceRpc<RawBlockTrace>('getblocktrace', [blockIndex]);
  return {
    blockIndex: result.block_index,
    blockHash: result.block_hash,
    transactions: (result.transactions ?? []).map((tx) => normalizeTransactionTrace(tx)),
  };
}

export async function fetchTransactionTrace(txHash: string): Promise<TransactionTraceResult> {
  const result = await callTraceRpc<RawTransactionTrace>('gettransactiontrace', [txHash]);
  return normalizeTransactionTrace(result);
}

export async function fetchContractCalls(contractHash: string): Promise<ContractCallGraph> {
  const result = await callTraceRpc<RawContractCallGraph>('getcontractcalls', [contractHash]);
  return {
    contractHash: result.contract_hash ?? contractHash,
    calls: (result.calls ?? []).map((call) =>
      normalizeContractCallTrace(call, call.tx_hash ?? contractHash, call.block_index ?? 0)
    ),
  };
}

export async function fetchSyscallStats(startBlock: number, endBlock: number): Promise<SyscallStat[]> {
  const result = await callTraceRpc<RawStatsResponse<RawSyscallStat> | RawSyscallStat[]>('getsyscallstats', [
    { startBlock, endBlock },
  ]);

  const statsArray = Array.isArray(result) ? result : result.stats ?? [];
  return statsArray.map((entry) => normalizeSyscallStat(entry));
}

export async function fetchOpCodeStats(startBlock: number, endBlock: number): Promise<OpCodeStat[]> {
  const result = await callTraceRpc<RawStatsResponse<RawOpCodeStat> | RawOpCodeStat[]>('getopcodestats', [
    { startBlock, endBlock },
  ]);

  const statsArray = Array.isArray(result) ? result : result.stats ?? [];
  return statsArray.map((entry) => normalizeOpCodeStat(entry));
}
