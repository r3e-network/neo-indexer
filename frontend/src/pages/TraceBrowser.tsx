import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  fetchBlockTrace,
  fetchContractCallStats,
  fetchContractCalls,
  fetchOpCodeStats,
  fetchSyscallStats,
  fetchTransactionTrace,
} from '../services/api';
import { OpCodeViewer } from '../components/traces/OpCodeViewer';
import { SyscallTimeline } from '../components/traces/SyscallTimeline';
import { CallGraph } from '../components/traces/CallGraph';
import { OpCodeStatsTable } from '../components/traces/OpCodeStatsTable';
import { ContractCallStatsTable } from '../components/traces/ContractCallStatsTable';
import type { ContractCallTraceEntry, SyscallStat, TransactionTraceResult } from '../types';

const tabs = [
  { id: 'opcodes', label: 'OpCode Trace' },
  { id: 'syscalls', label: 'Syscalls' },
  { id: 'callgraph', label: 'Contract Graph' },
] as const;

type TraceTab = (typeof tabs)[number]['id'];

const MAX_STATS_RANGE = 500_000;

export default function TraceBrowser() {
  const [mode, setMode] = useState<'block' | 'transaction'>('block');
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
  const [statsInput, setStatsInput] = useState({ start: '', end: '' });
  const [statsParams, setStatsParams] = useState<{ start?: number; end?: number }>({});
  const [statsValidationError, setStatsValidationError] = useState<string | null>(null);
  const [opcodeStatsParams, setOpcodeStatsParams] = useState<{ start?: number; end?: number }>({});
  const [contractCallStatsParams, setContractCallStatsParams] = useState<{ start?: number; end?: number }>({});

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

  return (
    <div className="mx-auto max-w-7xl space-y-6 px-4 py-8 text-white">
      <div className="rounded-3xl border border-slate-800 bg-slate-950/70 p-6 shadow-xl shadow-black/30">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-sm uppercase tracking-wide text-neo-green">Trace Browser</p>
            <h1 className="text-3xl font-semibold">Inspect OpCodes, syscalls, and contract interactions</h1>
            <p className="text-sm text-slate-400">
              Load traces for a block or transaction, then pivot across visualizations using the tabs below.
            </p>
          </div>
        </div>
        <form onSubmit={handleTraceSubmit} className="mt-6 space-y-4">
          <div className="flex flex-wrap items-center gap-3 text-sm text-slate-300">
            <span className="text-xs uppercase tracking-wide text-slate-500">Trace target</span>
            <label className="flex items-center gap-2">
              <input
                type="radio"
                name="trace-mode"
                value="block"
                checked={mode === 'block'}
                onChange={() => setMode('block')}
              />
              Block
            </label>
            <label className="flex items-center gap-2">
              <input
                type="radio"
                name="trace-mode"
                value="transaction"
                checked={mode === 'transaction'}
                onChange={() => setMode('transaction')}
              />
              Transaction
            </label>
          </div>
          <div className="grid gap-4 lg:grid-cols-[2fr,2fr,auto]">
            <div className="flex flex-col gap-1">
              <label htmlFor="blockIndex" className="text-xs uppercase tracking-wide text-slate-500">
                Block Index
              </label>
              <input
                id="blockIndex"
                type="number"
                min={0}
                value={blockInput}
                onChange={(event) => setBlockInput(event.target.value)}
                disabled={mode !== 'block'}
                className="rounded-xl border border-slate-800 bg-slate-900/80 px-4 py-2.5 text-white focus:border-neo-green focus:outline-none"
              />
            </div>
            <div className="flex flex-col gap-1">
              <label htmlFor="txHash" className="text-xs uppercase tracking-wide text-slate-500">
                Transaction Hash
              </label>
              <input
                id="txHash"
                type="text"
                value={txInput}
                onChange={(event) => setTxInput(event.target.value)}
                disabled={mode !== 'transaction'}
                placeholder="0x..."
                className="rounded-xl border border-slate-800 bg-slate-900/80 px-4 py-2.5 text-white focus:border-neo-green focus:outline-none"
              />
            </div>
            <div className="flex items-end">
              <button
                type="submit"
                className="w-full rounded-2xl bg-neo-green/90 px-6 py-3 font-semibold text-slate-900 transition hover:bg-neo-green"
              >
                {isTraceLoading ? 'Loading…' : 'Load Trace'}
              </button>
            </div>
          </div>
          {formError && <p className="text-sm text-rose-300">{formError}</p>}
          {mode === 'block' && blockTraceError && (
            <p className="text-sm text-rose-300">Unable to load block trace: {blockTraceError.message}</p>
          )}
          {mode === 'transaction' && txTraceError && (
            <p className="text-sm text-rose-300">Unable to load transaction trace: {txTraceError.message}</p>
          )}
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500">Opcode filters</p>
              <div className="mt-3 space-y-3">
                <input
                  type="text"
                  placeholder="Opcode name or mnemonic"
                  value={opcodeSearch}
                  onChange={(event) => setOpcodeSearch(event.target.value)}
                  className="w-full rounded-xl border border-slate-800 bg-slate-900/60 px-3 py-2 text-sm focus:border-neo-green focus:outline-none"
                />
                <label className="flex flex-col gap-1 text-xs text-slate-400">
                  Max stack depth ({stackDepthLimit})
                  <input
                    type="range"
                    min={1}
                    max={64}
                    value={stackDepthLimit}
                    onChange={(event) => setStackDepthLimit(Number(event.target.value))}
                    className="accent-neo-green"
                  />
                </label>
              </div>
            </div>
            <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500">Contract filter</p>
              <div className="mt-3 flex gap-2">
                <input
                  type="text"
                  placeholder="0x contract hash"
                  value={contractHashInput}
                  onChange={(event) => setContractHashInput(event.target.value)}
                  className="flex-1 rounded-xl border border-slate-800 bg-slate-900/60 px-3 py-2 text-sm focus:border-neo-green focus:outline-none"
                />
                <button
                  type="button"
                  onClick={handleContractGraphLoad}
                  className="rounded-xl border border-slate-800 px-4 py-2 text-sm text-slate-200 transition hover:border-neo-green/60"
                >
                  Load graph
                </button>
              </div>
              {contractError && <p className="mt-2 text-xs text-rose-300">Contract graph error: {contractError.message}</p>}
            </div>
            <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500">Stats range</p>
              <div className="mt-3 flex flex-col gap-2 text-sm text-slate-300">
                <div className="flex gap-2">
                  <input
                    type="number"
                    placeholder="Start block"
                    value={statsInput.start}
                    onChange={(event) => setStatsInput((prev) => ({ ...prev, start: event.target.value }))}
                    className="flex-1 rounded-xl border border-slate-800 bg-slate-900/60 px-3 py-2 text-sm focus:border-neo-green focus:outline-none"
                  />
                  <input
                    type="number"
                    placeholder="End block"
                    value={statsInput.end}
                    onChange={(event) => setStatsInput((prev) => ({ ...prev, end: event.target.value }))}
                    className="flex-1 rounded-xl border border-slate-800 bg-slate-900/60 px-3 py-2 text-sm focus:border-neo-green focus:outline-none"
                  />
                </div>
                <button
                  type="button"
                  onClick={handleStatsLoad}
                  className="rounded-xl bg-slate-800/80 px-4 py-2 text-sm text-white transition hover:bg-slate-700"
                >
                  Fetch syscall stats
                </button>
                <button
                  type="button"
                  onClick={handleOpcodeStatsLoad}
                  className="rounded-xl bg-slate-800/80 px-4 py-2 text-sm text-white transition hover:bg-slate-700"
                >
                  Fetch opcode stats
                </button>
                <button
                  type="button"
                  onClick={handleContractCallStatsLoad}
                  className="rounded-xl bg-slate-800/80 px-4 py-2 text-sm text-white transition hover:bg-slate-700"
                >
                  Fetch contract call stats
                </button>
                {statsValidationError && <p className="text-xs text-rose-300">{statsValidationError}</p>}
                {statsError && <p className="text-xs text-rose-300">Stats error: {statsError.message}</p>}
                {opcodeStatsError && (
                  <p className="text-xs text-rose-300">Opcode stats error: {opcodeStatsError.message}</p>
                )}
                {contractCallStatsError && (
                  <p className="text-xs text-rose-300">Contract call stats error: {contractCallStatsError.message}</p>
                )}
              </div>
            </div>
          </div>
        </form>
      </div>

      <div className="grid gap-6 lg:grid-cols-[320px,1fr]">
        <div className="space-y-4">
          <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-4">
            <h3 className="text-sm font-semibold">Transactions in block</h3>
            {mode === 'block' && blockTransactions.length > 0 ? (
              <div className="mt-3 flex max-h-[320px] flex-col gap-2 overflow-y-auto">
                {blockTransactions.map((tx) => {
                  const isActive = tx.txHash === selectedTxHash;
                  return (
                    <button
                      type="button"
                      key={tx.txHash}
                      onClick={() => setSelectedTxHash(tx.txHash)}
                      className={`rounded-xl border px-3 py-2 text-left text-sm transition ${
                        isActive ? 'border-neo-green/70 bg-neo-green/5' : 'border-slate-800 hover:border-slate-700'
                      }`}
                    >
                      <div className="font-mono text-xs text-slate-300">{tx.txHash.slice(0, 20)}…</div>
                      <div className="mt-1 flex gap-3 text-xs text-slate-500">
                        <span>{tx.opcodes.length} opcodes</span>
                        <span>{tx.syscalls.length} syscalls</span>
                      </div>
                    </button>
                  );
                })}
              </div>
            ) : (
              <p className="mt-3 text-sm text-slate-400">
                {mode === 'block' ? 'No block trace loaded yet.' : 'Switch to block mode to browse transactions.'}
              </p>
            )}
          </div>

          {summary && (
            <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-4 text-sm text-slate-200">
              <p className="text-xs uppercase tracking-wide text-slate-500">Trace summary</p>
              <p className="mt-2 font-mono text-xs text-slate-400">{summary.txHash}</p>
              <div className="mt-3 grid gap-3">
                <div className="flex items-center justify-between">
                  <span>Block index</span>
                  <span className="font-semibold text-white">#{summary.blockIndex.toLocaleString()}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span>OpCodes</span>
                  <span className="font-semibold text-white">{summary.opcodeCount.toLocaleString()}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span>Syscalls</span>
                  <span className="font-semibold text-white">{summary.syscallCount.toLocaleString()}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span>Contracts touched</span>
                  <span className="font-semibold text-white">{summary.contractCount}</span>
                </div>
              </div>
            </div>
          )}
        </div>

        <div className="space-y-4">
          <div className="flex flex-wrap gap-2 rounded-full border border-slate-800 bg-slate-950/70 p-1 text-sm">
            {tabs.map((tab) => (
              <button
                key={tab.id}
                type="button"
                onClick={() => setActiveTab(tab.id)}
                className={`flex-1 rounded-full px-4 py-2 transition ${
                  activeTab === tab.id ? 'bg-neo-green text-slate-900' : 'text-slate-300 hover:text-white'
                }`}
              >
                {tab.label}
              </button>
            ))}
          </div>

          {activeTab === 'opcodes' && (
            <div className="space-y-4">
              <OpCodeViewer
                traces={filteredOpcodes}
                isLoading={isTraceLoading}
                emptyMessage={currentTrace ? 'No opcodes match the current filters.' : 'Load a trace to begin.'}
              />
              <OpCodeStatsTable
                stats={activeOpcodeStats}
                isLoading={opcodeStatsQuery.isLoading || opcodeStatsQuery.isFetching}
                error={opcodeStatsError?.message ?? null}
              />
            </div>
          )}

          {activeTab === 'syscalls' && (
            <SyscallTimeline
              syscalls={currentTrace?.syscalls ?? []}
              stats={activeSyscallStats}
              isLoading={isTraceLoading}
              error={txTraceError?.message ?? blockTraceError?.message ?? null}
            />
          )}

          {activeTab === 'callgraph' && (
            <div className="space-y-4">
              <CallGraph
                calls={callGraphSource}
                highlightContract={contractQueryHash}
                isLoading={contractCallsQuery.isLoading || contractCallsQuery.isFetching}
              />
              <ContractCallStatsTable
                stats={activeContractCallStats}
                isLoading={contractCallStatsQuery.isLoading || contractCallStatsQuery.isFetching}
                error={contractCallStatsError?.message ?? null}
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
