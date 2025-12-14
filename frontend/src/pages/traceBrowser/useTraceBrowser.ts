import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import type { ContractCallTraceEntry, SyscallStat, TransactionTraceResult } from '../../types';
import { MAX_STATS_RANGE, type TraceMode, type TraceTab } from './constants';
import type { StatsRangeInput, StatsRangeParams } from './types';
import { useTraceBrowserQueries } from './useTraceBrowserQueries';

export function useTraceBrowser() {
  const [mode, setMode] = useState<TraceMode>('block');
  const [blockInput, setBlockInput] = useState('');
  const [txInput, setTxInput] = useState('');
  const [submittedBlock, setSubmittedBlock] = useState<number | null>(null);
  const [submittedTx, setSubmittedTx] = useState<string | null>(null);
  const [selectedTxHash, setSelectedTxHash] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<TraceTab>('opcodes');
  const [formError, setFormError] = useState<string | null>(null);
  const [opcodeSearch, setOpcodeSearch] = useState('');
  const [stackDepthLimit, setStackDepthLimit] = useState(32);
  const [contractHashInput, setContractHashInput] = useState('');
  const [contractQueryHash, setContractQueryHash] = useState<string | null>(null);
  const [statsInput, setStatsInput] = useState<StatsRangeInput>({ start: '', end: '' });
  const [statsParams, setStatsParams] = useState<StatsRangeParams>({});
  const [statsValidationError, setStatsValidationError] = useState<string | null>(null);
  const [opcodeStatsParams, setOpcodeStatsParams] = useState<StatsRangeParams>({});
  const [contractCallStatsParams, setContractCallStatsParams] = useState<StatsRangeParams>({});

  const {
    blockTraceQuery,
    transactionTraceQuery,
    contractCallsQuery,
    syscallStatsQuery,
    opcodeStatsQuery,
    contractCallStatsQuery,
  } = useTraceBrowserQueries({
    submittedBlock,
    submittedTx,
    contractQueryHash,
    statsParams,
    opcodeStatsParams,
    contractCallStatsParams,
  });

  useEffect(() => {
    if (mode === 'transaction' && submittedTx) {
      setSelectedTxHash(submittedTx);
    } else if (
      mode === 'block' &&
      blockTraceQuery.data?.transactions?.length &&
      !blockTraceQuery.data.transactions.some((tx) => tx.txHash === selectedTxHash)
    ) {
      setSelectedTxHash(blockTraceQuery.data.transactions[0].txHash);
    }
  }, [mode, submittedTx, blockTraceQuery.data, selectedTxHash]);

  const currentTrace: TransactionTraceResult | undefined = useMemo(() => {
    if (mode === 'transaction') {
      return transactionTraceQuery.data ?? undefined;
    }
    return blockTraceQuery.data?.transactions.find((tx) => tx.txHash === selectedTxHash);
  }, [mode, transactionTraceQuery.data, blockTraceQuery.data, selectedTxHash]);

  const normalizedContractFilter = contractHashInput.trim().toLowerCase();

  const filteredOpcodes = useMemo(() => {
    if (!currentTrace) return [];
    return currentTrace.opcodes.filter((opcode) => {
      if (normalizedContractFilter && !opcode.contractHash.toLowerCase().includes(normalizedContractFilter)) {
        return false;
      }
      if (
        opcodeSearch.trim() &&
        !(
          opcode.opcodeName.toLowerCase().includes(opcodeSearch.toLowerCase()) ||
          opcode.opcode.toLowerCase().includes(opcodeSearch.toLowerCase())
        )
      ) {
        return false;
      }
      return opcode.stackDepth <= stackDepthLimit;
    });
  }, [currentTrace, normalizedContractFilter, opcodeSearch, stackDepthLimit]);

  const callGraphSource = useMemo<ContractCallTraceEntry[]>(() => {
    if (contractCallsQuery.data?.calls?.length) {
      return contractCallsQuery.data.calls;
    }
    return currentTrace?.contractCalls ?? [];
  }, [contractCallsQuery.data, currentTrace]);

  const handleTraceSubmit = useCallback(
    (event: FormEvent) => {
      event.preventDefault();
      setFormError(null);

      if (mode === 'block') {
        const parsed = Number(blockInput);
        if (Number.isNaN(parsed) || parsed < 0) {
          setFormError('Enter a valid block index.');
          return;
        }
        setSubmittedBlock(parsed);
        setSubmittedTx(null);
        setSelectedTxHash(null);
      } else {
        const hash = txInput.trim();
        if (!hash) {
          setFormError('Enter a transaction hash.');
          return;
        }
        setSubmittedTx(hash);
        setSubmittedBlock(null);
        setSelectedTxHash(hash);
      }
    },
    [mode, blockInput, txInput]
  );

  const handleContractGraphLoad = useCallback(() => {
    setContractQueryHash(contractHashInput.trim() || null);
  }, [contractHashInput]);

  const validateStatsRange = useCallback(() => {
    const start = Number(statsInput.start);
    const end = Number(statsInput.end);
    if (Number.isNaN(start) || Number.isNaN(end) || start < 0 || end < start) {
      setStatsValidationError('Enter a valid block range.');
      return null;
    }
    if (end - start > MAX_STATS_RANGE) {
      setStatsValidationError(`Block range too large (max ${MAX_STATS_RANGE} blocks).`);
      return null;
    }
    setStatsValidationError(null);
    return { start, end };
  }, [statsInput]);

  const handleStatsLoad = useCallback(() => {
    const range = validateStatsRange();
    if (!range) return;
    setStatsParams(range);
  }, [validateStatsRange]);

  const handleOpcodeStatsLoad = useCallback(() => {
    const range = validateStatsRange();
    if (!range) return;
    setOpcodeStatsParams(range);
  }, [validateStatsRange]);

  const handleContractCallStatsLoad = useCallback(() => {
    const range = validateStatsRange();
    if (!range) return;
    setContractCallStatsParams(range);
  }, [validateStatsRange]);

  const isTraceLoading =
    mode === 'block'
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
    mode,
    setMode,
    blockInput,
    setBlockInput,
    txInput,
    setTxInput,
    submittedBlock,
    submittedTx,
    selectedTxHash,
    setSelectedTxHash,
    activeTab,
    setActiveTab,
    formError,
    opcodeSearch,
    setOpcodeSearch,
    stackDepthLimit,
    setStackDepthLimit,
    contractHashInput,
    setContractHashInput,
    contractQueryHash,
    statsInput,
    setStatsInput,
    statsValidationError,
    handleTraceSubmit,
    handleContractGraphLoad,
    handleStatsLoad,
    handleOpcodeStatsLoad,
    handleContractCallStatsLoad,
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
