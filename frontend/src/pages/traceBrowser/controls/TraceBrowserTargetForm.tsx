import type { TraceBrowserModel } from '../useTraceBrowser';

export function TraceBrowserTargetForm({ model }: { model: TraceBrowserModel }) {
  const {
    mode,
    setMode,
    blockInput,
    setBlockInput,
    txInput,
    setTxInput,
    isTraceLoading,
    formError,
    blockTraceError,
    txTraceError,
  } = model;

  return (
    <>
      <div className="flex flex-wrap items-center gap-3 text-sm text-slate-300">
        <span className="text-xs uppercase tracking-wide text-slate-500">Trace target</span>
        <label className="flex items-center gap-2">
          <input type="radio" name="trace-mode" value="block" checked={mode === 'block'} onChange={() => setMode('block')} />
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
    </>
  );
}

