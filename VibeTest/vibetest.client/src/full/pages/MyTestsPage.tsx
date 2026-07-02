import { useCallback, useEffect, useState } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router-dom';
import { LocalTestsList } from '@/components/tests/LocalTestsList';
import { Pagination } from '@/components/common/Pagination';
import { TestListProgressMeta } from '@/components/tests/TestListProgressMeta';
import { TestListItemCard } from '@/components/tests/TestListItemCard';
import { TestListToolbar } from '@/components/tests/TestListToolbar';
import { testsApi } from '@/full/api';
import { getApiErrorMessage, useAuth } from '@/full/context/AuthContext';
import type { TestListItem, TestProgressResponse } from '@/types';
import { editorPath, parseMyTestsTab, type MyTestsTab } from '@/utils/appPaths';
import { type PageSize } from '@/utils/pagination';
import { statsFromDbProgress } from '@/utils/playerHelpers';
import type { SortOrder, TestSortBy } from '@/utils/sortTests';
import {
  downloadCloudTestJson,
  saveCloudTestLocally,
  uploadLocalTestToCloud,
} from '@/utils/testTransfer';
import { downloadTestJson } from '@/utils/export';
import '@/components/tests/tests.css';

type Filter = 'all' | 'published' | 'private';

export function MyTestsPage() {
  const { isAuthenticated } = useAuth();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const tab: MyTestsTab = parseMyTestsTab(searchParams.toString());
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<PageSize>(10);
  const [sortBy, setSortBy] = useState<TestSortBy>('updatedAt');
  const [order, setOrder] = useState<SortOrder>('desc');
  const [filter, setFilter] = useState<Filter>('all');
  const [items, setItems] = useState<TestListItem[]>([]);
  const [progressByTestId, setProgressByTestId] = useState<Record<number, TestProgressResponse>>({});
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [hasPreviousPage, setHasPreviousPage] = useState(false);
  const [hasNextPage, setHasNextPage] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [actionId, setActionId] = useState<number | null>(null);
  const [uploadingLocalId, setUploadingLocalId] = useState<string | null>(null);
  const [localMessage, setLocalMessage] = useState<string | null>(null);

  const loadPage = useCallback(
    async (p: number, size: PageSize, f: Filter, nextSortBy: TestSortBy, nextOrder: SortOrder) => {
      setLoading(true);
      setError(null);
      try {
        const response = await testsApi.getMy(p, size, f, nextSortBy, nextOrder);
        setItems(response.items);
        setPage(response.page);
        setTotalCount(response.totalCount);
        setTotalPages(response.totalPages);
        setHasPreviousPage(response.hasPreviousPage);
        setHasNextPage(response.hasNextPage);

        if (response.items.length > 0) {
          const progress = await testsApi.getProgress(response.items.map((test) => test.id));
          setProgressByTestId(
            Object.fromEntries(progress.items.map((item) => [item.testId, item])) as Record<
              number,
              TestProgressResponse
            >,
          );
        } else {
          setProgressByTestId({});
        }
      } catch (err) {
        setError(getApiErrorMessage(err));
      } finally {
        setLoading(false);
      }
    },
    [],
  );

  useEffect(() => {
    if (tab !== 'cloud' || !isAuthenticated) {
      return;
    }
    void loadPage(page, pageSize, filter, sortBy, order);
  }, [loadPage, tab, isAuthenticated, page, pageSize, filter, sortBy, order]);

  async function runAction(id: number, action: () => Promise<unknown>) {
    setActionId(id);
    setError(null);
    try {
      await action();
      await loadPage(page, pageSize, filter, sortBy, order);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setActionId(null);
    }
  }

  function handlePageSizeChange(size: PageSize) {
    setPageSize(size);
    setPage(1);
  }

  function handleSortChange(nextSortBy: TestSortBy, nextOrder: SortOrder) {
    setSortBy(nextSortBy);
    setOrder(nextOrder);
    setPage(1);
  }

  function switchTab(nextTab: MyTestsTab) {
    setSearchParams({ tab: nextTab });
    if (nextTab === 'cloud') {
      setPage(1);
    }
  }

  async function handleDownloadCloudJson(id: number) {
    setActionId(id);
    setError(null);
    try {
      const full = await testsApi.getFull(id);
      downloadCloudTestJson(full);
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setActionId(null);
    }
  }

  async function handleSaveCloudLocally(id: number) {
    setActionId(id);
    setError(null);
    try {
      const full = await testsApi.getFull(id);
      saveCloudTestLocally(full);
      setLocalMessage(`Тест «${full.name}» сохранён локально.`);
      switchTab('local');
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setActionId(null);
    }
  }

  async function handleUploadLocalToCloud(testId: string) {
    setUploadingLocalId(testId);
    setLocalMessage(null);
    setError(null);
    try {
      await uploadLocalTestToCloud(testId);
      switchTab('cloud');
    } catch (err) {
      setError(getApiErrorMessage(err));
    } finally {
      setUploadingLocalId(null);
    }
  }

  return (
    <section className="full-page">
      <h1>Мои тесты</h1>

      <div className="full-filters" style={{ display: 'flex', gap: '0.5rem', margin: '1rem 0' }}>
        <button
          type="button"
          className={`full-button ${tab === 'local' ? '' : 'full-button--ghost'}`}
          onClick={() => switchTab('local')}
        >
          Локальные
        </button>
        <button
          type="button"
          className={`full-button ${tab === 'cloud' ? '' : 'full-button--ghost'}`}
          onClick={() => switchTab('cloud')}
        >
          Облачные
        </button>
      </div>

      {tab === 'local' && (
        <>
          <p className="full-muted">Локальные тесты из браузера.</p>
          <p>
            <Link to={editorPath()} className="full-button">
              Создать локальный тест
            </Link>
          </p>
          {localMessage && <p className="full-muted">{localMessage}</p>}
          {error && <p className="full-error">{error}</p>}
          <LocalTestsList
            listClassName="full-list"
            buttonClassPrefix="full-button"
            showCloudActions
            isAuthenticated={isAuthenticated}
            loginPath="/login"
            loginState={{ from: location.pathname + location.search }}
            uploadingId={uploadingLocalId}
            onDownloadJson={(test) => downloadTestJson(test)}
            onUploadToCloud={handleUploadLocalToCloud}
          />
        </>
      )}

      {tab === 'cloud' && (
        <>
          <p>
            <Link to={editorPath({ cloud: true })} className="full-button">
              Создать облачный тест
            </Link>
          </p>

          {!isAuthenticated ? (
            <p className="full-muted">
              Войдите, чтобы управлять облачными тестами.{' '}
              <Link to="/login" state={{ from: location.pathname + location.search }}>
                Вход
              </Link>
            </p>
          ) : (
            <>
              <div className="full-filters" style={{ display: 'flex', gap: '0.5rem', margin: '1rem 0' }}>
                {(['all', 'published', 'private'] as const).map((f) => (
                  <button
                    key={f}
                    type="button"
                    className={`full-button ${filter === f ? '' : 'full-button--ghost'}`}
                    onClick={() => {
                      setFilter(f);
                      setPage(1);
                    }}
                  >
                    {f === 'all' ? 'Все' : f === 'published' ? 'Опубликованные' : 'Черновики'}
                  </button>
                ))}
              </div>

              {loading && <p className="full-muted">Загрузка…</p>}
              {error && <p className="full-error">{error}</p>}

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
                  const stats = statsFromDbProgress(test.questionsCount, progressByTestId[test.id]);

                  return (
                    <TestListItemCard
                      key={test.id}
                      listClassName="full-list"
                      title={test.name}
                      difficulty={test.difficulty}
                      description={test.description}
                      badges={
                        <span className="vt-badge">{test.isPublic ? 'Опубликован' : 'Черновик'}</span>
                      }
                      meta={
                        <TestListProgressMeta
                          className="full-list__meta"
                          stats={stats}
                          suffix={<> · обновлён {new Date(test.updatedAt).toLocaleDateString()}</>}
                        />
                      }
                      actions={
                        <>
                          <Link to={`/tests/${test.id}/play`} className="full-button">
                            Пройти
                          </Link>
                          <Link to={`/editor/${test.id}`} className="full-button full-button--ghost">
                            Редактировать
                          </Link>
                          <button
                            type="button"
                            className="full-button full-button--ghost"
                            disabled={actionId === test.id || test.isPublic}
                            onClick={() => runAction(test.id, () => testsApi.publish(test.id))}
                          >
                            Опубликовать
                          </button>
                          <button
                            type="button"
                            className="full-button full-button--ghost"
                            disabled={actionId === test.id || !test.isPublic}
                            onClick={() => runAction(test.id, () => testsApi.unpublish(test.id))}
                          >
                            Скрыть
                          </button>
                          <button
                            type="button"
                            className="full-button full-button--ghost"
                            disabled={actionId === test.id}
                            onClick={() => void handleDownloadCloudJson(test.id)}
                          >
                            Скачать JSON
                          </button>
                          <button
                            type="button"
                            className="full-button full-button--ghost"
                            disabled={actionId === test.id}
                            onClick={() => void handleSaveCloudLocally(test.id)}
                          >
                            Сохранить локально
                          </button>
                          <button
                            type="button"
                            className="full-button full-button--danger"
                            disabled={actionId === test.id}
                            onClick={() => {
                              if (window.confirm(`Удалить тест «${test.name}»?`)) {
                                void runAction(test.id, () => testsApi.delete(test.id));
                              }
                            }}
                          >
                            Удалить
                          </button>
                        </>
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
            </>
          )}
        </>
      )}
    </section>
  );
}
