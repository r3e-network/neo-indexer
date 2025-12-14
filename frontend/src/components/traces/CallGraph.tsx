import { useEffect, useMemo, useRef } from 'react';
import type { ContractCallTraceEntry } from '../../types';
import { buildCallGraphData, formatContract } from './callGraph/graphData';
import { renderCallGraph } from './callGraph/renderCallGraph';
import { useCallGraphDimensions } from './callGraph/useCallGraphDimensions';

export interface CallGraphProps {
  calls?: ContractCallTraceEntry[];
  highlightContract?: string | null;
  isLoading?: boolean;
}

export function CallGraph({ calls = [], highlightContract, isLoading = false }: CallGraphProps) {
  const svgRef = useRef<SVGSVGElement | null>(null);
  const { containerRef, dimensions } = useCallGraphDimensions({ width: 900, height: 420 });

  const normalizedHighlight = highlightContract?.toLowerCase() ?? null;

  const { nodes, links } = useMemo(() => {
    return buildCallGraphData(calls);
  }, [calls]);

  useEffect(() => {
    if (!svgRef.current) return;
    return renderCallGraph(svgRef.current, {
      nodes,
      links,
      width: dimensions.width,
      height: dimensions.height,
      normalizedHighlight,
    });
  }, [nodes, links, dimensions.width, dimensions.height, normalizedHighlight]);

  if (isLoading) {
    return (
      <div className="flex min-h-[300px] items-center justify-center rounded-xl border border-slate-800 bg-slate-900/70">
        <span className="text-sm text-slate-400">Loading contract calls…</span>
      </div>
    );
  }

  if (!calls.length) {
    return (
      <div className="flex min-h-[200px] flex-col items-center justify-center rounded-xl border border-dashed border-slate-800 bg-slate-900/40 text-center text-slate-400">
        <p>No contract call traces available.</p>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-slate-800 bg-slate-950/60 p-4 shadow-lg shadow-black/40">
      <div className="flex items-center justify-between pb-4">
        <div>
          <h3 className="text-lg font-semibold text-white">Contract Call Graph</h3>
          <p className="text-sm text-slate-400">Force-directed layout grouped by call depth</p>
        </div>
        {highlightContract && (
          <div className="text-right text-xs text-slate-400">
            Highlighting <span className="font-mono text-violet-200">{formatContract(highlightContract)}</span>
          </div>
        )}
      </div>
      <div ref={containerRef} className="w-full">
        <svg ref={svgRef} width="100%" height={dimensions.height} role="img" />
      </div>
    </div>
  );
}
