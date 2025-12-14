import type { Selection } from './selection';

export function drag() {
  const behavior = (selection: Selection) => selection;
  (behavior as any).on = () => behavior;
  return behavior as any;
}

