import type { TraceBrowserModel } from './useTraceBrowser';

export function TraceBrowserControls({ model }: { model: TraceBrowserModel }) {
  const {
    mode,
    setMode,
    blockInput,
    setBlockInput,
    txInput,
    setTxInput,
    handleTraceSubmit,
    isTraceLoading,
    formError,
    blockTraceError,
    txTraceError,
    opcodeSearch,
    setOpcodeSearch,
    stackDepthLimit,
    setStackDepthLimit,
    contractHashInput,
    setContractHashInput,
    handleContractGraphLoad,
    contractError,
    statsInput,
    setStatsInput,
    handleStatsLoad,
    handleOpcodeStatsLoad,
    handleContractCallStatsLoad,
    statsValidationError,
    statsError,
    opcodeStatsError,
    contractCallStatsError,
  } = model;

  return (
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
              {opcodeStatsError && <p className="text-xs text-rose-300">Opcode stats error: {opcodeStatsError.message}</p>}
              {contractCallStatsError && (
                <p className="text-xs text-rose-300">Contract call stats error: {contractCallStatsError.message}</p>
              )}
            </div>
          </div>
        </div>
      </form>
    </div>
  );
}
