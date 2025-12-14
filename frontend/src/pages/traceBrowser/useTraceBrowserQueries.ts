import { useQuery } from '@tanstack/react-query';
import {
  fetchBlockTrace,
  fetchContractCallStats,
  fetchContractCalls,
  fetchOpCodeStats,
  fetchSyscallStats,
  fetchTransactionTrace,
} from '../../services/api';
import type { StatsRangeParams } from './types';

export function useTraceBrowserQueries({
  submittedBlock,
  submittedTx,
  contractQueryHash,
  statsParams,
  opcodeStatsParams,
  contractCallStatsParams,
}: {
  submittedBlock: number | null;
  submittedTx: string | null;
  contractQueryHash: string | null;
  statsParams: StatsRangeParams;
  opcodeStatsParams: StatsRangeParams;
  contractCallStatsParams: StatsRangeParams;
}) {
  const blockTraceQuery = useQuery({
    queryKey: ['block-trace', submittedBlock],
    queryFn: () => fetchBlockTrace(submittedBlock!),
    enabled: submittedBlock !== null,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });

  const transactionTraceQuery = useQuery({
    queryKey: ['transaction-trace', submittedTx],
    queryFn: () => fetchTransactionTrace(submittedTx!),
    enabled: Boolean(submittedTx),
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });

  const contractCallsQuery = useQuery({
    queryKey: ['contract-calls', contractQueryHash],
    queryFn: () => fetchContractCalls(contractQueryHash!),
    enabled: Boolean(contractQueryHash),
    staleTime: 300_000,
  });

  const syscallStatsQuery = useQuery({
    queryKey: ['syscall-stats', statsParams.start, statsParams.end],
    queryFn: () => fetchSyscallStats(statsParams.start!, statsParams.end!),
    enabled: typeof statsParams.start === 'number' && typeof statsParams.end === 'number',
    staleTime: 300_000,
  });

  const opcodeStatsQuery = useQuery({
    queryKey: ['opcode-stats', opcodeStatsParams.start, opcodeStatsParams.end],
    queryFn: () => fetchOpCodeStats(opcodeStatsParams.start!, opcodeStatsParams.end!),
    enabled: typeof opcodeStatsParams.start === 'number' && typeof opcodeStatsParams.end === 'number',
    staleTime: 300_000,
  });

  const contractCallStatsQuery = useQuery({
    queryKey: ['contract-call-stats', contractCallStatsParams.start, contractCallStatsParams.end, contractQueryHash],
    queryFn: () =>
      fetchContractCallStats(contractCallStatsParams.start!, contractCallStatsParams.end!, {
        calleeHash: contractQueryHash || undefined,
      }),
    enabled: typeof contractCallStatsParams.start === 'number' && typeof contractCallStatsParams.end === 'number',
    staleTime: 300_000,
  });

  return {
    blockTraceQuery,
    transactionTraceQuery,
    contractCallsQuery,
    syscallStatsQuery,
    opcodeStatsQuery,
    contractCallStatsQuery,
  };
}

