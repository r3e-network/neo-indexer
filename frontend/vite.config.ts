import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = fileURLToPath(new URL('.', import.meta.url));

export default defineConfig(({ mode }) => {
  const isTest = mode === 'test';
  const aliasEntries = [
    { find: 'msw/node', replacement: path.resolve(__dirname, './src/test/mswNodeShim.ts') },
    { find: 'msw', replacement: path.resolve(__dirname, './src/test/mswShim.ts') },
  ];

  if (isTest) {
    aliasEntries.push(
      { find: 'd3', replacement: path.resolve(__dirname, './src/test/d3TestShim.ts') },
      {
        find: '@tanstack/react-query',
        replacement: path.resolve(__dirname, './src/test/reactQueryStub.tsx'),
      }
    );
  }

  return {
    plugins: [react()],
    resolve: {
      alias: aliasEntries,
    },
    server: {
      port: 3000,
      host: true,
    },
    test: {
      globals: true,
      environment: 'jsdom',
      setupFiles: './src/test/setup.ts',
      coverage: {
        provider: 'v8',
        reporter: ['text', 'text-summary', 'lcov'],
        include: ['src/**/*.{ts,tsx}'],
        exclude: ['src/test/**', 'src/main.tsx', 'src/vite-env.d.ts'],
      },
    },
  };
});
