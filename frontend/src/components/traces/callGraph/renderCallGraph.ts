import * as d3 from 'd3';
import type { GraphLink, GraphNode } from './graphData';

export function renderCallGraph(
  svgElement: SVGSVGElement,
  options: { nodes: GraphNode[]; links: GraphLink[]; width: number; height: number; normalizedHighlight: string | null }
) {
  const { nodes, links, width, height, normalizedHighlight } = options;
  const svg = d3.select(svgElement);
  svg.selectAll('*').remove();

  if (!nodes.length) {
    svg.append('text').attr('x', 24).attr('y', 40).attr('fill', '#94a3b8').text('No contract calls recorded.');
    return () => undefined;
  }

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
}

