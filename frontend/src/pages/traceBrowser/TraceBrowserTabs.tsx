import { OpCodeViewer } from '../../components/traces/OpCodeViewer';
import { SyscallTimeline } from '../../components/traces/SyscallTimeline';
import { CallGraph } from '../../components/traces/CallGraph';
import { OpCodeStatsTable } from '../../components/traces/OpCodeStatsTable';
import { ContractCallStatsTable } from '../../components/traces/ContractCallStatsTable';
import { TRACE_TABS } from './constants';
import type { TraceBrowserModel } from './useTraceBrowser';

export function TraceBrowserTabs({ model }: { model: TraceBrowserModel }) {
  const {
    activeTab,
    setActiveTab,
    filteredOpcodes,
    isTraceLoading,
    currentTrace,
    activeOpcodeStats,
    opcodeStatsIsLoading,
    opcodeStatsError,
    activeSyscallStats,
    txTraceError,
    blockTraceError,
    callGraphSource,
    contractQueryHash,
    contractCallsIsLoading,
    activeContractCallStats,
    contractCallStatsIsLoading,
    contractCallStatsError,
  } = model;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-2 rounded-full border border-slate-800 bg-slate-950/70 p-1 text-sm">
        {TRACE_TABS.map((tab) => (
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
          <OpCodeStatsTable stats={activeOpcodeStats} isLoading={opcodeStatsIsLoading} error={opcodeStatsError?.message ?? null} />
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
          <CallGraph calls={callGraphSource} highlightContract={contractQueryHash} isLoading={contractCallsIsLoading} />
          <ContractCallStatsTable
            stats={activeContractCallStats}
            isLoading={contractCallStatsIsLoading}
            error={contractCallStatsError?.message ?? null}
          />
        </div>
      )}
    </div>
  );
}
