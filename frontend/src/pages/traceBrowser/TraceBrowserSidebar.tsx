import type { TraceBrowserModel } from './useTraceBrowser';

export function TraceBrowserSidebar({ model }: { model: TraceBrowserModel }) {
  const { mode, blockTransactions, selectedTxHash, setSelectedTxHash, summary } = model;

  return (
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
  );
}
