/**
 * Rate limiter unit tests
 * Run with: npm test
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';

// Mock NextRequest and NextResponse
const mockRequest = (ip: string = '127.0.0.1') => ({
  ip,
  headers: {
    get: (key: string) => key === 'x-forwarded-for' ? ip : null,
  },
  url: 'http://localhost/api/test',
});

describe('Rate Limiter', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('should allow requests under the limit', async () => {
    const { rateLimit } = await import('../lib/rate-limit');
    const req = mockRequest() as any;

    // First request should pass
    const result = rateLimit(req, 10, 60000);
    expect(result).toBeNull();
  });

  it('should block requests over the limit', async () => {
    const { rateLimit } = await import('../lib/rate-limit');
    const req = mockRequest('192.168.1.1') as any;

    // Exhaust the limit
    for (let i = 0; i < 10; i++) {
      rateLimit(req, 10, 60000);
    }

    // Next request should be blocked
    const result = rateLimit(req, 10, 60000);
    expect(result).not.toBeNull();
  });

  it('should track different IPs separately', async () => {
    const { rateLimit } = await import('../lib/rate-limit');
    const req1 = mockRequest('10.0.0.1') as any;
    const req2 = mockRequest('10.0.0.2') as any;

    // Exhaust limit for IP 1
    for (let i = 0; i < 5; i++) {
      rateLimit(req1, 5, 60000);
    }

    // IP 2 should still be allowed
    const result = rateLimit(req2, 5, 60000);
    expect(result).toBeNull();
  });
});
