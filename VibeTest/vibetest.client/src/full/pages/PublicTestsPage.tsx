import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Pagination } from '@/components/common/Pagination';
import { TestListProgressMeta } from '@/components/tests/TestListProgressMeta';
import { TestListItemCard } from '@/components/tests/TestListItemCard';
import { TestListToolbar } from '@/components/tests/TestListToolbar';
import { testsApi } from '@/full/api';
import { getApiErrorMessage, useAuth } from '@/full/context/AuthContext';
import type { TestListItem, TestProgressResponse } from '@/types';
import { type PageSize } from '@/utils/pagination';
import { getTestProgressStats, statsFromDbProgress } from '@/utils/playerHelpers';
import type { SortOrder, TestSortBy } from '@/utils/sortTests';
import { getApiTestProgress } from '@/utils/storage';
import '@/components/tests/tests.css';

export function PublicTestsPage() {
  const { isAuthenticated } = useAuth();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<PageSize>(10);
  const [sortBy, setSortBy] = useState<TestSortBy>('updatedAt');
  const [order, setOrder] = useState<SortOrder>('desc');
  const [items, setItems] = useState<TestListItem[]>([]);
  const [progressByTestId, setProgressByTestId] = useState<Record<number, TestProgressResponse>>({});
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [hasPreviousPage, setHasPreviousPage] = useState(false);
  const [hasNextPage, setHasNextPage] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadPage = useCallback(
    async (p: number, size: PageSize, nextSortBy: TestSortBy, nextOrder: SortOrder) => {
      setLoading(true);
      setError(null);
      try {
        const response = await testsApi.getPublic(p, size, nextSortBy, nextOrder);
        setItems(response.items);
        setPage(response.page);
        setTotalCount(response.totalCount);
        setTotalPages(response.totalPages);
        setHasPreviousPage(response.hasPreviousPage);
        setHasNextPage(response.hasNextPage);

        if (isAuthenticated && response.items.length > 0) {
          const progress = await testsApi.getProgress(response.items.map((test) => test.id));
          const nextProgress = Object.fromEntries(
            progress.items.map((item) => [item.testId, item]),
          ) as Record<number, TestProgressResponse>;
          setProgressByTestId(nextProgress);
        } else {
          setProgressByTestId({});
        }
      } catch (err) {
        setError(getApiErrorMessage(err));
      } finally {
        setLoading(false);
      }
    },
    [isAuthenticated],
  );

  useEffect(() => {
    void loadPage(page, pageSize, sortBy, order);
  }, [loadPage, page, pageSize, sortBy, order]);

  const statsForTest = useMemo(
    () => (test: TestListItem) => {
      if (isAuthenticated) {
        return statsFromDbProgress(test.questionsCount, progressByTestId[test.id]);
      }
      return getTestProgressStats(test.questionsCount, getApiTestProgress(test.id));
    },
    [isAuthenticated, progressByTestId],
  );

  function handlePageSizeChange(size: PageSize) {
    setPageSize(size);
    setPage(1);
  }

  function handleSortChange(nextSortBy: TestSortBy, nextOrder: SortOrder) {
    setSortBy(nextSortBy);
    setOrder(nextOrder);
    setPage(1);
  }

  return (
    <section className="full-page">
      <h1>Публичные тесты</h1>

      {loading && <p className="full-muted">Загрузка…</p>}
      {error && <p className="full-error">{error}</p>}

      {!loading && !error && totalCount === 0 && (
        <p className="full-muted">Публичных тестов пока нет.</p>
      )}

      {!loading && !error && totalCount > 0 && (
        <TestListToolbar
          totalCount={totalCount}
          pageSize={pageSize}
          onPageSizeChange={handlePageSizeChange}
          sortBy={sortBy}
          order={order}
          onSortChange={handleSortChange}
        />
      )}

      <ul className="full-list">
        {items.map((test) => {
          const stats = statsForTest(test);

          return (
            <TestListItemCard
              key={test.id}
              listClassName="full-list"
              title={test.name}
              difficulty={test.difficulty}
              description={test.description}
              meta={
                <TestListProgressMeta
                  className="full-list__meta"
                  stats={stats}
                  suffix={
                    <>
                      {' '}
                      · {test.authorName} · обновлён{' '}
                      {new Date(test.updatedAt).toLocaleDateString()}
                    </>
                  }
                />
              }
              actions={
                <Link to={`/tests/${test.id}/play`} className="full-button">
                  Пройти тест
                </Link>
              }
            />
          );
        })}
      </ul>

      <Pagination
        page={page}
        totalPages={totalPages}
        hasPreviousPage={hasPreviousPage}
        hasNextPage={hasNextPage}
        onPageChange={setPage}
      />
    </section>
  );
}
