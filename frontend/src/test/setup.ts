import '@testing-library/jest-dom';

// Mock import.meta.env
const testEnv = {
  VITE_SUPABASE_URL: 'https://test.supabase.co',
  VITE_SUPABASE_ANON_KEY: 'test-key',
  VITE_SUPABASE_BUCKET: 'block-state',
  VITE_TRACE_RPC_URL: 'http://localhost:10332',
  VITE_TRACE_API_KEY: 'test-trace-key',
};

Object.defineProperty(import.meta, 'env', {
  value: testEnv,
  configurable: true,
});

(globalThis as any).__vitest_env__ = testEnv;

if (typeof process !== 'undefined' && process.env) {
  for (const [key, value] of Object.entries(testEnv)) {
    if (process.env[key] === undefined) {
      process.env[key] = value;
    }
  }
}

// Basic ResizeObserver polyfill for jsdom so D3 visualizations can mount in tests
if (!('ResizeObserver' in globalThis)) {
  class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  globalThis.ResizeObserver = ResizeObserver;
}
