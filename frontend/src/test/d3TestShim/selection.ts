import type { Datum, DatumElement } from './types';

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
  return parent.ownerDocument!.createElementNS(namespace, tag) as DatumElement;
}

function setAttribute(element: DatumElement, name: string, value: any) {
  const resolved = typeof value === 'function' ? value(element.__datum__, element) : value;
  if (resolved !== undefined) {
    element.setAttribute(name, String(resolved));
  }
}

function setText(element: DatumElement, value: any) {
  const resolved = typeof value === 'function' ? value(element.__datum__, element) : value;
  if (resolved !== undefined) {
    element.textContent = String(resolved);
  }
}

export class Selection implements SelectionLike {
  constructor(private elements: DatumElement[], private parents?: Element[]) {}

  selectAll(selector: string) {
    const matches = this.elements.flatMap((element) => Array.from(element.querySelectorAll(selector)));
    return new Selection(matches as DatumElement[], this.elements);
  }

  append(tag: string) {
    const created = this.elements.map((element) => {
      const child = createSvgElement(tag, element);
      child.__datum__ = element.__datum__;
      element.appendChild(child);
      return child;
    });
    return new Selection(created);
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

export class DataSelection {
  constructor(private parents: DatumElement[], private dataArray: Datum[]) {}

  join(tag: string) {
    const created = this.dataArray.map((datum, index) => {
      const parent = this.parents[Math.min(index, this.parents.length - 1)];
      const element = createSvgElement(tag, parent);
      element.__datum__ = datum;
      parent.appendChild(element);
      return element;
    });
    return new Selection(created);
  }
}

export function select(element: Element) {
  return new Selection([element as DatumElement]);
}

