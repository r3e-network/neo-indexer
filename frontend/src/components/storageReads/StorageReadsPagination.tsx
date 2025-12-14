export function StorageReadsPagination({
  page,
  totalPages,
  totalCount,
  loading,
  onPageChange,
}: {
  page: number;
  totalPages: number;
  totalCount: number;
  loading: boolean;
  onPageChange: (page: number) => void;
}) {
  if (totalPages <= 1) return null;

  return (
    <div className="pagination">
      <button onClick={() => onPageChange(1)} disabled={page === 1 || loading}>
        First
      </button>
      <button onClick={() => onPageChange(page - 1)} disabled={page === 1 || loading}>
        Previous
      </button>
      <span>
        Page {page} of {totalPages} ({totalCount} total)
      </span>
      <button onClick={() => onPageChange(page + 1)} disabled={page === totalPages || loading}>
        Next
      </button>
      <button onClick={() => onPageChange(totalPages)} disabled={page === totalPages || loading}>
        Last
      </button>
    </div>
  );
}

