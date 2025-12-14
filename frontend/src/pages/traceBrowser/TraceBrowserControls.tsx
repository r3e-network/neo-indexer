import type { TraceBrowserModel } from './useTraceBrowser';
import { TraceBrowserFilters } from './controls/TraceBrowserFilters';
import { TraceBrowserTargetForm } from './controls/TraceBrowserTargetForm';

export function TraceBrowserControls({ model }: { model: TraceBrowserModel }) {
  const { handleTraceSubmit } = model;

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
        <TraceBrowserTargetForm model={model} />
        <TraceBrowserFilters model={model} />
      </form>
    </div>
  );
}
