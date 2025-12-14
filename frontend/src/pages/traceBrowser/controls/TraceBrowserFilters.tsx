import type { TraceBrowserModel } from '../useTraceBrowser';

export function TraceBrowserFilters({ model }: { model: TraceBrowserModel }) {
  const {
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
  );
}

