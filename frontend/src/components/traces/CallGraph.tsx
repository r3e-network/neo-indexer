import { useEffect, useMemo, useRef, useState } from 'react';
import * as d3 from 'd3';
import type { ContractCallTraceEntry } from '../../types';

interface GraphNode extends d3.SimulationNodeDatum {
  id: string;
  label: string;
  hash: string | null;
  depth: number;
  totalGas: number;
  callCount: number;
}

interface GraphLink extends d3.SimulationLinkDatum<GraphNode> {
  source: string | GraphNode;
  target: string | GraphNode;
  methodName?: string | null;
  gas: number;
  count: number;
}

export interface CallGraphProps {
  calls?: ContractCallTraceEntry[];
  highlightContract?: string | null;
  isLoading?: boolean;
}

function formatContract(hash: string | null) {
  if (!hash) return 'Entry';
  return `${hash.slice(0, 8)}…${hash.slice(-6)}`;
}

export function CallGraph({ calls = [], highlightContract, isLoading = false }: CallGraphProps) {
  const svgRef = useRef<SVGSVGElement | null>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [dimensions, setDimensions] = useState({ width: 900, height: 420 });

  const normalizedHighlight = highlightContract?.toLowerCase() ?? null;

  useEffect(() => {
    if (!containerRef.current) return;
    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (entry) {
        setDimensions((prev) => ({
          width: entry.contentRect.width,
          height: prev.height,
        }));
      }
    });
    observer.observe(containerRef.current);
    return () => observer.disconnect();
  }, []);

  const { nodes, links } = useMemo(() => {
    const nodeMap = new Map<string, GraphNode>();
    const linkMap = new Map<string, GraphLink>();

    const ensureNode = (id: string, hash: string | null, depth: number): GraphNode => {
      if (!nodeMap.has(id)) {
        nodeMap.set(id, {
          id,
          label: formatContract(hash),
          hash,
          depth,
          totalGas: 0,
          callCount: 0,
        });
      }
      return nodeMap.get(id)!;
    };

    calls.forEach((call) => {
      const callerId = call.callerHash ?? `entry-${call.txHash}`;
      const calleeId = call.calleeHash;
      const callerNode = ensureNode(callerId, call.callerHash, Math.max(call.callDepth - 1, 0));
      const calleeNode = ensureNode(calleeId, call.calleeHash, call.callDepth);
      callerNode.callCount += 1;
      callerNode.totalGas += call.gasConsumed ?? 0;

      const linkKey = `${callerNode.id}->${calleeNode.id}:${call.methodName ?? 'invoke'}`;
      if (!linkMap.has(linkKey)) {
        linkMap.set(linkKey, {
          source: callerNode.id,
          target: calleeNode.id,
          methodName: call.methodName,
          gas: call.gasConsumed ?? 0,
          count: 1,
        });
      } else {
        const link = linkMap.get(linkKey)!;
        link.count += 1;
        link.gas += call.gasConsumed ?? 0;
      }
    });

    return {
      nodes: Array.from(nodeMap.values()),
      links: Array.from(linkMap.values()),
    };
  }, [calls]);

  useEffect(() => {
    if (!svgRef.current) return;
    const svg = d3.select(svgRef.current);
    svg.selectAll('*').remove();

    if (!nodes.length) {
      svg.append('text').attr('x', 24).attr('y', 40).attr('fill', '#94a3b8').text('No contract calls recorded.');
      return;
    }

    const width = dimensions.width;
    const height = dimensions.height;

    svg.attr('viewBox', `0 0 ${width} ${height}`);

    const simulation = d3
      .forceSimulation(nodes)
      .force(
        'link',
        d3
          .forceLink<GraphNode, GraphLink>(links)
          .id((node) => node.id)
          .distance(160)
          .strength(0.6)
      )
      .force('charge', d3.forceManyBody().strength(-300))
      .force('center', d3.forceCenter(width / 2, height / 2))
      .force('collision', d3.forceCollide<GraphNode>().radius((node) => 30 + node.depth * 5));

    const link = svg
      .append('g')
      .attr('stroke', '#475569')
      .attr('stroke-opacity', 0.6)
      .selectAll('line')
      .data(links)
      .join('line')
      .attr('stroke-width', (d) => 1 + Math.log2(d.count + 1));

    const node = svg
      .append('g')
      .attr('stroke', '#0f172a')
      .attr('stroke-width', 1.5)
      .selectAll<SVGCircleElement, GraphNode>('circle')
      .data(nodes)
      .join('circle')
      .attr('r', (d) => 18 + d.depth * 4)
      .attr('fill', (d) => {
        if (d.hash && normalizedHighlight && d.hash.toLowerCase() === normalizedHighlight) {
          return '#c084fc';
        }
        return d.depth === 0 ? '#38bdf8' : '#22c55e';
      })
      .call(
        d3
          .drag<SVGCircleElement, GraphNode>()
          .on('start', (event, datum) => {
            if (!event.active) simulation.alphaTarget(0.3).restart();
            datum.fx = datum.x;
            datum.fy = datum.y;
          })
          .on('drag', (event, datum) => {
            datum.fx = event.x;
            datum.fy = event.y;
          })
          .on('end', (event, datum) => {
            if (!event.active) simulation.alphaTarget(0);
            datum.fx = null;
            datum.fy = null;
          })
      );

    node.append('title').text((d) => `${d.label}\nCalls: ${d.callCount}\nGAS: ${(d.totalGas / 1e8).toFixed(3)} GAS`);

    const labels = svg
      .append('g')
      .selectAll('text')
      .data(nodes)
      .join('text')
      .attr('text-anchor', 'middle')
      .attr('fill', '#e2e8f0')
      .attr('font-size', 12)
      .text((d) => d.label);

    const linkLabels = svg
      .append('g')
      .selectAll('text')
      .data(links)
      .join('text')
      .attr('fill', '#94a3b8')
      .attr('font-size', 10)
      .text((d) => d.methodName ?? 'invoke');

    simulation.on('tick', () => {
      link
        .attr('x1', (d) => (typeof d.source !== 'string' ? d.source.x ?? 0 : 0))
        .attr('y1', (d) => (typeof d.source !== 'string' ? d.source.y ?? 0 : 0))
        .attr('x2', (d) => (typeof d.target !== 'string' ? d.target.x ?? 0 : 0))
        .attr('y2', (d) => (typeof d.target !== 'string' ? d.target.y ?? 0 : 0));

      node.attr('cx', (d) => d.x ?? 0).attr('cy', (d) => d.y ?? 0);

      labels
        .attr('x', (d) => d.x ?? 0)
        .attr('y', (d) => (d.y ?? 0) - (20 + d.depth * 4));

      linkLabels
        .attr('x', (d) =>
          typeof d.source !== 'string' && typeof d.target !== 'string'
            ? ((d.source.x ?? 0) + (d.target.x ?? 0)) / 2
            : 0
        )
        .attr('y', (d) =>
          typeof d.source !== 'string' && typeof d.target !== 'string'
            ? ((d.source.y ?? 0) + (d.target.y ?? 0)) / 2
            : 0
        );
    });

    return () => {
      simulation.stop();
    };
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
