import type { Datum } from './types';

export function forceSimulation(nodes: Datum[]) {
  nodes.forEach((node, index) => {
    node.x = 120 + index * 40;
    node.y = 120 + (index % 2) * 30;
  });

  const api = {
    force() {
      return api;
    },
    on(event: string, handler: () => void) {
      if (event === 'tick') {
        handler();
      }
      return api;
    },
    stop() {},
    alphaTarget() {
      return api;
    },
    restart() {
      return api;
    },
  };
  return api;
}

export function forceLink() {
  const api = {
    id() {
      return api;
    },
    distance() {
      return api;
    },
    strength() {
      return api;
    },
  };
  return api;
}

export function forceManyBody() {
  return {
    strength() {
      return this;
    },
  };
}

export function forceCenter() {
  return {
    x() {
      return this;
    },
    y() {
      return this;
    },
  };
}

export function forceCollide() {
  return {
    radius() {
      return this;
    },
  };
}

