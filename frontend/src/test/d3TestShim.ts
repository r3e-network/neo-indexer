type Datum = Record<string, any>;

interface SelectionLike {
  selectAll(selector: string): Selection;
  append(tag: string): Selection;
  attr(name: string, value: any): Selection;
  text(value: any): Selection;
  data(dataArray: Datum[]): DataSelection;
  call(fn: (selection: Selection) => void): Selection;
  remove(): Selection;
}

function createSvgElement(tag: string, parent: Element) {
  const namespace = parent.namespaceURI ?? 'http://www.w3.org/2000/svg';
  return parent.ownerDocument!.createElementNS(namespace, tag) as Element & { __datum__?: Datum };
}

function setAttribute(element: Element & { __datum__?: Datum }, name: string, value: any) {
  const resolved = typeof value === 'function' ? value(element.__datum__, element) : value;
  if (resolved !== undefined) {
    element.setAttribute(name, String(resolved));
  }
}

function setText(element: Element & { __datum__?: Datum }, value: any) {
  const resolved = typeof value === 'function' ? value(element.__datum__, element) : value;
  if (resolved !== undefined) {
    element.textContent = String(resolved);
  }
}

class Selection implements SelectionLike {
  constructor(private elements: (Element & { __datum__?: Datum })[], private parents?: Element[]) {}

  selectAll(selector: string) {
    const matches = this.elements.flatMap((element) => Array.from(element.querySelectorAll(selector)));
    return new Selection(matches as Element[], this.elements);
  }

  append(tag: string) {
    const created = this.elements.map((element) => {
      const child = createSvgElement(tag, element);
      child.__datum__ = element.__datum__;
      element.appendChild(child);
      return child;
    });
    return new Selection(created as Element[]);
  }

  attr(name: string, value: any) {
    this.elements.forEach((element) => setAttribute(element, name, value));
    return this;
  }

  text(value: any) {
    this.elements.forEach((element) => setText(element, value));
    return this;
  }

  data(dataArray: Datum[]) {
    return new DataSelection(this.parents ?? this.elements, dataArray);
  }

  call(fn: (selection: Selection) => void) {
    fn(this);
    return this;
  }

  remove() {
    this.elements.forEach((element) => element.remove());
    return this;
  }
}

class DataSelection {
  constructor(private parents: (Element & { __datum__?: Datum })[], private dataArray: Datum[]) {}

  join(tag: string) {
    const created = this.dataArray.map((datum, index) => {
      const parent = this.parents[Math.min(index, this.parents.length - 1)];
      const element = createSvgElement(tag, parent);
      element.__datum__ = datum;
      parent.appendChild(element);
      return element;
    });
    return new Selection(created as Element[]);
  }
}

export function select(element: Element) {
  return new Selection([element as Element]);
}

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

export function drag() {
  const behavior = (selection: Selection) => selection;
  (behavior as any).on = () => behavior;
  return behavior as any;
}
