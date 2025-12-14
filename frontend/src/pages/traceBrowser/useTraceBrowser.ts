import { useEffect, useMemo } from 'react';
import type { ContractCallTraceEntry, SyscallStat, TransactionTraceResult } from '../../types';
import { useTraceBrowserQueries } from './useTraceBrowserQueries';
import { useTraceBrowserState } from './useTraceBrowserState';

export function useTraceBrowser() {
  const state = useTraceBrowserState();

  const {
    blockTraceQuery,
    transactionTraceQuery,
    contractCallsQuery,
    syscallStatsQuery,
    opcodeStatsQuery,
    contractCallStatsQuery,
  } = useTraceBrowserQueries({
    submittedBlock: state.submittedBlock,
    submittedTx: state.submittedTx,
    contractQueryHash: state.contractQueryHash,
    statsParams: state.statsParams,
    opcodeStatsParams: state.opcodeStatsParams,
    contractCallStatsParams: state.contractCallStatsParams,
  });

  useEffect(() => {
    if (state.mode === 'transaction' && state.submittedTx) {
      state.setSelectedTxHash(state.submittedTx);
    } else if (
      state.mode === 'block' &&
      blockTraceQuery.data?.transactions?.length &&
      !blockTraceQuery.data.transactions.some((tx) => tx.txHash === state.selectedTxHash)
    ) {
      state.setSelectedTxHash(blockTraceQuery.data.transactions[0].txHash);
    }
  }, [state.mode, state.submittedTx, blockTraceQuery.data, state.selectedTxHash, state.setSelectedTxHash]);

  const currentTrace: TransactionTraceResult | undefined = useMemo(() => {
    if (state.mode === 'transaction') {
      return transactionTraceQuery.data ?? undefined;
    }
    return blockTraceQuery.data?.transactions.find((tx) => tx.txHash === state.selectedTxHash);
  }, [state.mode, transactionTraceQuery.data, blockTraceQuery.data, state.selectedTxHash]);

  const normalizedContractFilter = state.contractHashInput.trim().toLowerCase();

  const filteredOpcodes = useMemo(() => {
    if (!currentTrace) return [];
    return currentTrace.opcodes.filter((opcode) => {
      if (normalizedContractFilter && !opcode.contractHash.toLowerCase().includes(normalizedContractFilter)) {
        return false;
      }
      if (
        state.opcodeSearch.trim() &&
        !(
          opcode.opcodeName.toLowerCase().includes(state.opcodeSearch.toLowerCase()) ||
          opcode.opcode.toLowerCase().includes(state.opcodeSearch.toLowerCase())
        )
      ) {
        return false;
      }
      return opcode.stackDepth <= state.stackDepthLimit;
    });
  }, [currentTrace, normalizedContractFilter, state.opcodeSearch, state.stackDepthLimit]);

  const callGraphSource = useMemo<ContractCallTraceEntry[]>(() => {
    if (contractCallsQuery.data?.calls?.length) {
      return contractCallsQuery.data.calls;
    }
    return currentTrace?.contractCalls ?? [];
  }, [contractCallsQuery.data, currentTrace]);

  const isTraceLoading =
    state.mode === 'block'
      ? blockTraceQuery.isLoading || blockTraceQuery.isFetching
      : transactionTraceQuery.isLoading || transactionTraceQuery.isFetching;

  const blockTraceError = blockTraceQuery.error as Error | null;
  const txTraceError = transactionTraceQuery.error as Error | null;
  const contractError = contractCallsQuery.error as Error | null;
  const statsError = syscallStatsQuery.error as Error | null;
  const opcodeStatsError = opcodeStatsQuery.error as Error | null;
  const contractCallStatsError = contractCallStatsQuery.error as Error | null;

  const blockTransactions = blockTraceQuery.data?.transactions ?? [];

  const summary = useMemo(() => {
    if (!currentTrace) return null;
    const uniqueContracts = new Set(currentTrace.opcodes.map((trace) => trace.contractHash));
    return {
      txHash: currentTrace.txHash,
      blockIndex: currentTrace.blockIndex,
      opcodeCount: currentTrace.opcodes.length,
      syscallCount: currentTrace.syscalls.length,
      contractCount: uniqueContracts.size,
    };
  }, [currentTrace]);

  const activeSyscallStats: SyscallStat[] = syscallStatsQuery.data ?? [];
  const activeOpcodeStats = opcodeStatsQuery.data ?? [];
  const activeContractCallStats = contractCallStatsQuery.data ?? [];

  return {
    ...state,
    isTraceLoading,
    blockTraceError,
    txTraceError,
    contractError,
    statsError,
    opcodeStatsError,
    contractCallStatsError,
    blockTransactions,
    currentTrace,
    filteredOpcodes,
    callGraphSource,
    summary,
    activeSyscallStats,
    activeOpcodeStats,
    activeContractCallStats,
    opcodeStatsIsLoading: opcodeStatsQuery.isLoading || opcodeStatsQuery.isFetching,
    contractCallsIsLoading: contractCallsQuery.isLoading || contractCallsQuery.isFetching,
    contractCallStatsIsLoading: contractCallStatsQuery.isLoading || contractCallStatsQuery.isFetching,
  };
}

export type TraceBrowserModel = ReturnType<typeof useTraceBrowser>;
