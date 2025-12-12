import type { MockHandler } from './mswShim';

type RequestInfoType = Request | string | URL;

function buildRequest(input: RequestInfoType, init?: RequestInit) {
  if (input instanceof Request) {
    return init ? new Request(input, init) : input.clone();
  }
  return new Request(input, init);
}

export function setupServer(...initialHandlers: MockHandler[]) {
  let handlers = [...initialHandlers];
  const defaultHandlers = [...initialHandlers];
  let originalFetch: typeof globalThis.fetch | null = null;

  const dispatchRequest = async (input: RequestInfoType, init?: RequestInit) => {
    const request = buildRequest(input, init);
    const handler = handlers.find((candidate) => candidate.method === request.method && candidate.matcher(request));
    if (handler) {
      return handler.resolver({ request });
    }
    if (!originalFetch) {
      throw new Error('Fetch has not been patched by setupServer.listen()');
    }
    return originalFetch(input, init);
  };

  return {
    listen() {
      if (originalFetch) {
        return;
      }
      originalFetch = globalThis.fetch.bind(globalThis);
      globalThis.fetch = dispatchRequest;
    },
    use(...nextHandlers: MockHandler[]) {
      handlers.push(...nextHandlers);
    },
    resetHandlers(...nextHandlers: MockHandler[]) {
      handlers = nextHandlers.length ? [...nextHandlers] : [...defaultHandlers];
    },
    close() {
      if (originalFetch) {
        globalThis.fetch = originalFetch;
        originalFetch = null;
      }
    },
  };
}
