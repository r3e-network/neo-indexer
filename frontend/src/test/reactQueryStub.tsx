import { createContext, useContext, useEffect, useMemo, useState } from 'react';

type QueryOptions<TData> = {
  queryKey: unknown[];
  queryFn: () => Promise<TData> | TData;
  enabled?: boolean;
};

type QueryResult<TData> = {
  data: TData | undefined;
  error: Error | null;
  isLoading: boolean;
  isFetching: boolean;
};

class QueryClient {
  clear() {}
}

const QueryClientContext = createContext<QueryClient | null>(null);

export { QueryClient };

export function QueryClientProvider({
  client,
  children,
}: {
  client: QueryClient;
  children: React.ReactNode;
}) {
  return <QueryClientContext.Provider value={client}>{children}</QueryClientContext.Provider>;
}

export function useQuery<TData>(options: QueryOptions<TData>): QueryResult<TData> {
  const enabled = options.enabled ?? true;
  useContext(QueryClientContext); // Access to ensure provider exists

  const [state, setState] = useState<QueryResult<TData>>({
    data: undefined,
    error: null,
    isLoading: enabled,
    isFetching: enabled,
  });

  const keySignature = useMemo(() => JSON.stringify(options.queryKey), [options.queryKey]);

  useEffect(() => {
    let isActive = true;

    if (!enabled) {
      setState((prev) => ({ ...prev, isLoading: false, isFetching: false }));
      return;
    }

    setState((prev) => ({ ...prev, isLoading: true, isFetching: true }));

    Promise.resolve()
      .then(() => options.queryFn())
      .then((result) => {
        if (isActive) {
          setState({ data: result, error: null, isLoading: false, isFetching: false });
        }
      })
      .catch((error: Error) => {
        if (isActive) {
          setState({ data: undefined, error, isLoading: false, isFetching: false });
        }
      });

    return () => {
      isActive = false;
    };
  }, [keySignature, enabled]);

  return state;
}
