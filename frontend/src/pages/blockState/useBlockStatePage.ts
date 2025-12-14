import { useCallback, useState } from 'react';
import { getBlockByIndex, searchBlocks } from '../../services/supabase';
import type { Block, BlockSearchFilters, PaginatedResponse } from '../../types';

export function useBlockStatePage() {
  const [blocks, setBlocks] = useState<PaginatedResponse<Block> | null>(null);
  const [selectedBlock, setSelectedBlock] = useState<Block | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [currentFilters, setCurrentFilters] = useState<BlockSearchFilters>({});

  const handleSearch = useCallback(async (filters: BlockSearchFilters, pageNum = 1) => {
    setLoading(true);
    setError(null);
    setCurrentFilters(filters);
    setPage(pageNum);

    try {
      if (filters.blockIndex !== undefined && !filters.hash && !filters.startDate && !filters.endDate) {
        const block = await getBlockByIndex(filters.blockIndex);
        if (block) {
          setBlocks({ data: [block], count: 1, page: 1, pageSize: 50, hasMore: false });
          setSelectedBlock(block);
        } else {
          setBlocks({ data: [], count: 0, page: 1, pageSize: 50, hasMore: false });
          setSelectedBlock(null);
        }
        return;
      }

      const result = await searchBlocks(filters, pageNum);
      setBlocks(result);
      if (result.data.length === 1) {
        setSelectedBlock(result.data[0]);
      } else {
        setSelectedBlock(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Search failed');
      setBlocks(null);
    } finally {
      setLoading(false);
    }
  }, []);

  const handlePageChange = useCallback(
    (newPage: number) => {
      handleSearch(currentFilters, newPage);
    },
    [currentFilters, handleSearch]
  );

  const handleBlockSelect = useCallback((block: Block) => {
    setSelectedBlock(block);
  }, []);

  return {
    blocks,
    selectedBlock,
    loading,
    error,
    page,
    currentFilters,
    handleSearch,
    handlePageChange,
    handleBlockSelect,
  };
}

export type BlockStatePageModel = ReturnType<typeof useBlockStatePage>;

