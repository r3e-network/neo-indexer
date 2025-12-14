import { useEffect, useRef, useState } from 'react';

export function useCallGraphDimensions(initial: { width: number; height: number }) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [dimensions, setDimensions] = useState(initial);

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

  return { containerRef, dimensions };
}

