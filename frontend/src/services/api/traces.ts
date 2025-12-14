import type { BlockTraceResult, ContractCallGraph, TransactionTraceResult } from '../../types';
import { getSupabase } from '../supabase';
import { fetchAllTracesByBlock, fetchAllTracesByTx } from './pagination';
import {
  normalizeContractCallTrace,
  normalizeOpCodeTrace,
  normalizeSyscallTrace,
  type RawContractCallTrace,
  type RawOpCodeTrace,
  type RawSyscallTrace,
} from './normalize';

export async function fetchBlockTrace(blockIndex: number): Promise<BlockTraceResult> {
  const supabase = getSupabase();

  const [{ data: blockRow, error: blockError }, opcodes, syscalls, calls] = await Promise.all([
    supabase.from('blocks').select('hash').eq('block_index', blockIndex).maybeSingle(),
    fetchAllTracesByBlock<RawOpCodeTrace>('opcode_traces', blockIndex),
    fetchAllTracesByBlock<RawSyscallTrace>('syscall_traces', blockIndex),
    fetchAllTracesByBlock<RawContractCallTrace>('contract_calls', blockIndex),
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
    fetchAllTracesByTx<RawOpCodeTrace>('opcode_traces', txHash),
    fetchAllTracesByTx<RawSyscallTrace>('syscall_traces', txHash),
    fetchAllTracesByTx<RawContractCallTrace>('contract_calls', txHash),
  ]);

  const blockIndex = opcodes[0]?.block_index ?? syscalls[0]?.block_index ?? calls[0]?.block_index ?? null;

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
    normalizeContractCallTrace(row as RawContractCallTrace, (row as RawContractCallTrace).tx_hash ?? contractHash, (row as RawContractCallTrace).block_index ?? 0)
  );

  return {
    contractHash,
    calls,
  };
}

