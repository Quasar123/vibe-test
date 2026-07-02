import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Pagination } from '@/components/common/Pagination';
import { resultsApi, testsApi } from '@/full/api';
import { getApiErrorMessage } from '@/full/context/AuthContext';
import type { TestHistoryItem, UserStatsResponse } from '@/types';

const PAGE_SIZE = 10;

export function ProfilePage() {
  const [stats, setStats] = useState<UserStatsResponse | null>(null);
  const [history, setHistory] = useState<TestHistoryItem[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [hasPreviousPage, setHasPreviousPage] = useState(false);
  const [hasNextPage, setHasNextPage] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadHistory = useCallback(async (p: number) => {
    const historyData = await resultsApi.getHistory(p, PAGE_SIZE);
    setHistory(historyData.items);
    setPage(historyData.page);
    setTotalPages(historyData.totalPages);
    setHasPreviousPage(historyData.hasPreviousPage);
    setHasNextPage(historyData.hasNextPage);
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const [statsData] = await Promise.all([testsApi.getMyStats()]);
        if (!cancelled) {
          setStats(statsData);
        }
        await loadHistory(page);
      } catch (err) {
        if (!cancelled) {
          setError(getApiErrorMessage(err));
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [loadHistory, page]);

  return (
    <section className="full-page">
      <h1>Профиль</h1>
      {error && <p className="full-error">{error}</p>}

      {stats && (
        <>
          <h2 className="full-muted" style={{ marginTop: '1.5rem', marginBottom: '0.75rem' }}>
            Мои тесты
          </h2>
          <div className="full-stats">
            <div className="full-stat">
              <div className="full-stat__value">{stats.totalCreated}</div>
              <div className="full-stat__label">Создано</div>
            </div>
            <div className="full-stat">
              <div className="full-stat__value">{stats.totalPublished}</div>
              <div className="full-stat__label">Опубликовано</div>
            </div>
          </div>

          <h2 className="full-muted" style={{ marginTop: '1.5rem', marginBottom: '0.75rem' }}>
            Прохождение своих тестов
          </h2>
          <div className="full-stats">
            <div className="full-stat">
              <div className="full-stat__value">{stats.totalPassedOwn}</div>
              <div className="full-stat__label">Пройдено</div>
            </div>
            <div className="full-stat">
              <div className="full-stat__value">{stats.averageScoreOwn.toFixed(1)}%</div>
              <div className="full-stat__label">Средний балл</div>
            </div>
          </div>

          <h2 className="full-muted" style={{ marginTop: '1.5rem', marginBottom: '0.75rem' }}>
            Прохождение чужих тестов
          </h2>
          <div className="full-stats">
            <div className="full-stat">
              <div className="full-stat__value">{stats.totalPassedOthers}</div>
              <div className="full-stat__label">Пройдено</div>
            </div>
            <div className="full-stat">
              <div className="full-stat__value">{stats.averageScoreOthers.toFixed(1)}%</div>
              <div className="full-stat__label">Средний балл</div>
            </div>
          </div>
        </>
      )}

      <h2 style={{ marginTop: '2rem' }}>История</h2>
      {history.length === 0 ? (
        <p className="full-muted">Пока нет завершённых прохождений.</p>
      ) : (
        <ul className="full-list">
          {history.map((item) => (
            <li key={`${item.testId}-${item.completedAt}`} className="full-list__item">
              <Link to={`/tests/${item.testId}/play`} className="full-list__title">
                {item.testName}
              </Link>
              <div className="full-list__meta">
                {item.correctAnswers}/{item.totalQuestions} · {item.scorePercent.toFixed(0)}% ·{' '}
                {new Date(item.completedAt).toLocaleString()}
              </div>
            </li>
          ))}
        </ul>
      )}

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
