type Matcher = string | RegExp | ((request: Request) => boolean);

export type MockResolver = (args: { request: Request }) => Response | Promise<Response>;

export interface MockHandler {
  method: string;
  matcher: (request: Request) => boolean;
  resolver: MockResolver;
}

function createMatcher(input: Matcher): (request: Request) => boolean {
  if (typeof input === 'function') {
    return input;
  }
  if (input instanceof RegExp) {
    return (request) => input.test(request.url);
  }
  return (request) => {
    try {
      const expected = new URL(input, request.url);
      const actual = new URL(request.url);
      return expected.href === actual.href;
    } catch (error) {
      // Fallback to simple comparison when URL construction fails (e.g. relative URLs)
      return request.url === input;
    }
  };
}

function normalizeMethod(method: string) {
  return method.toUpperCase();
}

function createHandler(method: string, matcher: Matcher, resolver: MockResolver): MockHandler {
  return {
    method: normalizeMethod(method),
    matcher: createMatcher(matcher),
    resolver,
  };
}

type HttpFactory = {
  get: (matcher: Matcher, resolver: MockResolver) => MockHandler;
  post: (matcher: Matcher, resolver: MockResolver) => MockHandler;
  put: (matcher: Matcher, resolver: MockResolver) => MockHandler;
  patch: (matcher: Matcher, resolver: MockResolver) => MockHandler;
  delete: (matcher: Matcher, resolver: MockResolver) => MockHandler;
};

export const http: HttpFactory = {
  get: (matcher, resolver) => createHandler('GET', matcher, resolver),
  post: (matcher, resolver) => createHandler('POST', matcher, resolver),
  put: (matcher, resolver) => createHandler('PUT', matcher, resolver),
  patch: (matcher, resolver) => createHandler('PATCH', matcher, resolver),
  delete: (matcher, resolver) => createHandler('DELETE', matcher, resolver),
};

export const HttpResponse = {
  json(body: unknown, init: ResponseInit = {}) {
    const headers = new Headers(init.headers);
    if (!headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json');
    }
    return new Response(JSON.stringify(body), {
      ...init,
      headers,
    });
  },
};
