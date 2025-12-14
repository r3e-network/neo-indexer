import type { ContractCallStat, OpCodeStat, SyscallStat } from '../../types';
import { getSupabase } from '../supabase';
import {
  normalizeContractCallStat,
  normalizeOpCodeStat,
  normalizeSyscallStat,
  type RawContractCallStat,
  type RawOpCodeStat,
  type RawSyscallStat,
} from './normalize';

export async function fetchSyscallStats(startBlock: number, endBlock: number): Promise<SyscallStat[]> {
  const supabase = getSupabase();
  const { data, error } = await supabase.rpc('get_syscall_stats', {
    start_block: startBlock,
    end_block: endBlock,
  });

  if (error) {
    throw new Error(`Failed to fetch syscall stats: ${error.message}`);
  }

  const entries = (data as RawSyscallStat[] | null) ?? [];
  return entries.map((entry) => normalizeSyscallStat(entry));
}

export async function fetchOpCodeStats(startBlock: number, endBlock: number): Promise<OpCodeStat[]> {
  const supabase = getSupabase();
  const { data, error } = await supabase.rpc('get_opcode_stats', {
    start_block: startBlock,
    end_block: endBlock,
  });

  if (error) {
    throw new Error(`Failed to fetch opcode stats: ${error.message}`);
  }

  const entries = (data as RawOpCodeStat[] | null) ?? [];
  return entries.map((entry) => normalizeOpCodeStat(entry));
}

export async function fetchContractCallStats(
  startBlock: number,
  endBlock: number,
  options: { calleeHash?: string; callerHash?: string; methodName?: string } = {}
): Promise<ContractCallStat[]> {
  const supabase = getSupabase();
  const { data, error } = await supabase.rpc('get_contract_call_stats', {
    start_block: startBlock,
    end_block: endBlock,
    p_callee_hash: options.calleeHash ?? null,
    p_caller_hash: options.callerHash ?? null,
    p_method_name: options.methodName ?? null,
  });

  if (error) {
    throw new Error(`Failed to fetch contract call stats: ${error.message}`);
  }

  const entries = (data as RawContractCallStat[] | null) ?? [];
  return entries.map((entry) => normalizeContractCallStat(entry));
}

