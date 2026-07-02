import { TestResultSummary } from '@/components/tests/TestResultSummary';
import { PlayerSettings } from '@/components/tests/player/PlayerSettings';
import { QuestionCard } from '@/components/tests/player/QuestionCard';
import { QuestionNav } from '@/components/tests/player/QuestionNav';
import type { TestPlayerProps, TestPlayerSource } from '@/components/tests/player/types';
import { useTestPlayerController } from '@/components/tests/player/useTestPlayerController';
import '@/components/tests/tests.css';

export type { TestPlayerSource };

export function TestPlayer({ source, onExit }: TestPlayerProps) {
  const player = useTestPlayerController(source);

  if (player.error && player.phase === 'loading') {
    return <p className="vt-error">{player.error}</p>;
  }

  if (player.phase === 'loading') {
    return <p className="vt-muted">Загрузка теста…</p>;
  }

  if (player.phase === 'completed') {
    return (
      <TestResultSummary
        {...player.completedSummary}
        onReviewAnswers={player.canReviewAnswers ? player.handleReviewAnswers : undefined}
        onRetry={player.canRetry ? () => void player.handleRetry() : undefined}
        onExit={onExit}
      />
    );
  }

  if (!player.currentQuestion) {
    return <p className="vt-error">В тесте нет вопросов</p>;
  }

  return (
    <div
      className="vt-player"
      onCopy={(e) => e.preventDefault()}
      onCut={(e) => e.preventDefault()}
    >
      <header>
        <h2>{player.testName}</h2>
        {player.hasNewQuestions && (
          <p className="vt-muted">В тест добавлены новые вопросы — дорешайте их.</p>
        )}
        {!player.applicationHideResults && (
          <PlayerSettings
            settings={player.explanationSettings}
            onChange={player.updateExplanationSettings}
          />
        )}
      </header>

      <QuestionNav
        questions={player.questions}
        progress={player.progress}
        currentOrder={player.currentOrder}
        hideResults={player.applicationHideResults}
        onSelect={player.goToQuestion}
      />

      <QuestionCard
        currentOrder={player.currentOrder}
        currentQuestion={player.currentQuestion}
        questionsCount={player.questions.length}
        phase={player.phase}
        showingResult={player.showingResult}
        isChecking={player.isChecking}
        showNavOverlay={player.showNavOverlay}
        selectedOrder={player.selectedOrder}
        activeRecord={player.activeRecord ?? null}
        applicationHideResults={player.applicationHideResults}
        explanationText={player.explanationText}
        error={player.error}
        onAnswer={(order) => void player.handleAnswer(order)}
        onNext={player.handleNext}
      />

      <div className="vt-player__toolbar">
        <div className="vt-player__nav">
          <button
            type="button"
            className="vt-btn vt-btn--ghost"
            onClick={() => player.goToQuestion(player.currentOrder - 1)}
            disabled={!player.canGoBack || player.isChecking}
          >
            Назад
          </button>
          <button
            type="button"
            className="vt-btn"
            onClick={player.handleNext}
            disabled={!player.canGoNext || player.isChecking}
          >
            Далее
          </button>
        </div>
        {onExit && (
          <button
            type="button"
            className="vt-btn vt-btn--ghost vt-player__exit-btn"
            onClick={onExit}
            disabled={player.isChecking}
          >
            Выйти
          </button>
        )}
      </div>
    </div>
  );
}
