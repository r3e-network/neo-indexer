import { type FormEvent, useCallback, useState } from 'react';
import { MAX_STATS_RANGE, type TraceMode, type TraceTab } from './constants';
import type { StatsRangeInput, StatsRangeParams } from './types';

export function useTraceBrowserState() {
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
    statsParams,
    statsValidationError,
    opcodeStatsParams,
    contractCallStatsParams,
    handleTraceSubmit,
    handleContractGraphLoad,
    handleStatsLoad,
    handleOpcodeStatsLoad,
    handleContractCallStatsLoad,
  };
}

export type TraceBrowserState = ReturnType<typeof useTraceBrowserState>;

