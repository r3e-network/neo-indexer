"use client";

import { useState } from "react";

export function CopyButton({ value, label }: { value: string; label?: string }) {
  const [copied, setCopied] = useState(false);
  const text = label || "Copy";

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      setCopied(false);
    }
  };

  return (
    <button
      type="button"
      onClick={handleCopy}
      className="ml-2 px-2 py-1 text-[11px] rounded border border-slate-800 hover:border-green-500 text-slate-400 hover:text-white transition-colors"
      aria-label={`Copy ${text}`}
    >
      {copied ? "Copied" : "Copy"}
    </button>
  );
}
