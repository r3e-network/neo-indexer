import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";

// Simple in-memory token bucket per IP for Netlify Edge/SSR runtime.
// Note: This is best-effort and non-distributed.
const buckets = new Map<string, { tokens: number; last: number }>();

// Prevent memory leak: cap max entries and cleanup stale buckets
const MAX_BUCKETS = 10000;
const STALE_MS = 300_000; // 5 minutes

function cleanupBuckets(now: number) {
  if (buckets.size > MAX_BUCKETS) {
    const staleThreshold = now - STALE_MS;
    for (const [ip, bucket] of buckets) {
      if (bucket.last < staleThreshold) {
        buckets.delete(ip);
      }
    }
    // If still over limit, remove oldest entries
    if (buckets.size > MAX_BUCKETS) {
      const entries = Array.from(buckets.entries()).sort((a, b) => a[1].last - b[1].last);
      const toRemove = entries.slice(0, buckets.size - MAX_BUCKETS + 1000);
      for (const [ip] of toRemove) {
        buckets.delete(ip);
      }
    }
  }
}

export function rateLimit(req: NextRequest, limit = 60, windowMs = 60_000) {
  const ip = req.ip || req.headers.get("x-forwarded-for") || "unknown";
  const now = Date.now();

  // Periodic cleanup to prevent memory leak
  if (buckets.size > MAX_BUCKETS * 0.9) {
    cleanupBuckets(now);
  }

  const bucket = buckets.get(ip) || { tokens: limit, last: now };

  const elapsed = now - bucket.last;
  const refill = Math.floor(elapsed / windowMs) * limit;
  bucket.tokens = Math.min(limit, bucket.tokens + refill);
  bucket.last = now;

  if (bucket.tokens <= 0) {
    return NextResponse.json({ error: "rate_limited" }, { status: 429 });
  }

  bucket.tokens -= 1;
  buckets.set(ip, bucket);
  return null;
}
