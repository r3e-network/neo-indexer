import type { SimulationLinkDatum, SimulationNodeDatum } from 'd3';
import type { ContractCallTraceEntry } from '../../../types';

export interface GraphNode extends SimulationNodeDatum {
  id: string;
  label: string;
  hash: string | null;
  depth: number;
  totalGas: number;
  callCount: number;
}

export interface GraphLink extends SimulationLinkDatum<GraphNode> {
  source: string | GraphNode;
  target: string | GraphNode;
  methodName?: string | null;
  gas: number;
  count: number;
}

export function formatContract(hash: string | null) {
  if (!hash) return 'Entry';
  return `${hash.slice(0, 8)}…${hash.slice(-6)}`;
}

export function buildCallGraphData(calls: ContractCallTraceEntry[]): { nodes: GraphNode[]; links: GraphLink[] } {
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
}

