import { useCallback, useEffect, useMemo, useState } from 'react';
import { getApiErrorMessage } from '@/full/context/AuthContext';
import type { QuestionDefinition } from '@/types';
import type { TestResultResponse } from '@/types';
import type { PlayerPhase, PlayerProgress, PlayerQuestion } from '@/types/player';
import {
  fetchApiResult,
  fetchApplicationResult,
  isAlreadyAnsweredError,
  loadPlayerState,
  resetPlayerAttempt,
  restoreProgressFromServer,
  shouldPersistRestoredProgress,
  submitPlayerAnswer,
  canRestoreFromServerOnDuplicate,
} from '@/components/tests/player/playerSources';
import {
  emptyProgress,
  isAuthenticatedApiSource,
  isGuestApiSource,
  persistProgress,
} from '@/components/tests/player/progress';
import type { CompletedSummaryProps, TestPlayerSource } from '@/components/tests/player/types';
import { countCorrect, isTestFullyAnswered } from '@/utils/playerHelpers';
import {
  getPlayerExplanationSettings,
  savePlayerExplanationSettings,
  shouldShowExplanation,
  type PlayerExplanationSettings,
} from '@/utils/playerSettings';
import { clearApplicationProgress, saveGuestResult } from '@/utils/storage';

export function useTestPlayerController(source: TestPlayerSource) {
  const [phase, setPhase] = useState<PlayerPhase>('loading');
  const [testName, setTestName] = useState('');
  const [questions, setQuestions] = useState<PlayerQuestion[]>([]);
  const [localDefinitions, setLocalDefinitions] = useState<QuestionDefinition[]>([]);
  const [progress, setProgress] = useState<PlayerProgress>(emptyProgress);
  const [selectedOrder, setSelectedOrder] = useState<number | null>(null);
  const [apiResult, setApiResult] = useState<TestResultResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [hasNewQuestions, setHasNewQuestions] = useState(false);
  const [explanationSettings, setExplanationSettings] = useState<PlayerExplanationSettings>(
    getPlayerExplanationSettings,
  );
  const [applicationHideResults, setApplicationHideResults] = useState(
    () => source.type === 'application' && Boolean(source.hideResults),
  );
  const [applicationIsCompleted, setApplicationIsCompleted] = useState(
    () => source.type === 'application' && Boolean(source.isCompleted),
  );

  const currentOrder = progress.currentQuestionOrder;
  const currentQuestion = questions[currentOrder];
  const currentRecord = progress.answers[currentOrder];
  const showingResult = phase === 'result' && Boolean(currentRecord);

  const refreshPhase = useCallback(
    (
      qs: PlayerQuestion[],
      prog: PlayerProgress,
      result: TestResultResponse | null,
      options?: { hideResults?: boolean; serverCompleted?: boolean },
    ) => {
      const answered = Object.keys(prog.answers).length;
      const total = qs.length;
      const hideResults = options?.hideResults ?? false;
      const serverCompleted = options?.serverCompleted ?? false;

      if (serverCompleted || (hideResults && answered >= total && total > 0)) {
        setPhase('completed');
        return;
      }

      if (result && result.totalQuestions < total && answered >= result.totalQuestions) {
        setHasNewQuestions(true);
        setPhase('unresolved');
        return;
      }

      if (result?.completedAt && answered >= total) {
        setPhase('completed');
        return;
      }

      if (answered >= total && total > 0) {
        setPhase('completed');
        return;
      }

      setPhase(prog.answers[prog.currentQuestionOrder] ? 'result' : 'answering');
    },
    [],
  );

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const loaded = await loadPlayerState(source);
        if (cancelled) return;

        setTestName(loaded.testName);
        setQuestions(loaded.questions);
        setLocalDefinitions(loaded.localDefinitions);
        setProgress(loaded.progress);
        setApiResult(loaded.apiResult);
        if (loaded.applicationHideResults !== undefined) {
          setApplicationHideResults(loaded.applicationHideResults);
        }
        if (loaded.applicationIsCompleted !== undefined) {
          setApplicationIsCompleted(loaded.applicationIsCompleted);
        }
        refreshPhase(loaded.questions, loaded.progress, loaded.apiResult, loaded.refreshOptions);
      } catch (err) {
        if (!cancelled) setError(getApiErrorMessage(err));
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [source, refreshPhase]);

  useEffect(() => {
    if (phase === 'completed' && source.type === 'local' && questions.length > 0) {
      const correct = countCorrect(progress);
      saveGuestResult({
        testId: source.testId,
        testName,
        totalQuestions: questions.length,
        correctAnswers: correct,
        completedAt: new Date().toISOString(),
      });
    }
  }, [phase, source, progress, questions.length, testName]);

  const completedSummary = useMemo((): CompletedSummaryProps => {
    if (source.type === 'application' && applicationHideResults && phase === 'completed') {
      return {
        testName,
        totalQuestions: questions.length,
        correctAnswers: 0,
        variant: 'submitted',
      };
    }
    if ((source.type === 'api' || source.type === 'application') && apiResult && phase === 'completed') {
      return {
        testName: apiResult.testName,
        totalQuestions: apiResult.totalQuestions,
        correctAnswers: apiResult.correctAnswers,
        completedAt: apiResult.completedAt,
        variant: 'full',
      };
    }
    if (source.type === 'application' && applicationIsCompleted && phase === 'completed') {
      return {
        testName,
        totalQuestions: questions.length,
        correctAnswers: 0,
        variant: 'submitted',
      };
    }
    return {
      testName,
      totalQuestions: questions.length,
      correctAnswers: countCorrect(progress),
      completedAt: new Date().toISOString(),
      variant: 'full',
    };
  }, [
    apiResult,
    applicationHideResults,
    applicationIsCompleted,
    phase,
    progress,
    questions.length,
    source.type,
    testName,
  ]);

  const goToQuestion = useCallback(
    (order: number) => {
      if (order < 0 || order >= questions.length) return;
      const next: PlayerProgress = {
        ...progress,
        currentQuestionOrder: order,
        updatedAt: new Date().toISOString(),
      };
      setProgress(next);
      persistProgress(source, next);
      setSelectedOrder(next.answers[order]?.selectedAnswerOrder ?? null);
      setPhase(next.answers[order] ? 'result' : 'answering');
    },
    [progress, questions.length, source],
  );

  const handleReviewAnswers = useCallback(() => {
    goToQuestion(0);
  }, [goToQuestion]);

  const canReviewAnswers =
    !applicationHideResults && Object.keys(progress.answers).length > 0;

  const completeTest = useCallback(async () => {
    if (source.type === 'api') {
      if (isGuestApiSource(source)) {
        setPhase('completed');
      } else {
        const result = await fetchApiResult(source);
        setApiResult(result);
        setPhase('completed');
      }
      return;
    }

    if (source.type === 'application') {
      if (applicationHideResults) {
        setApplicationIsCompleted(true);
        clearApplicationProgress(source.token);
        setPhase('completed');
      } else {
        try {
          const result = await fetchApplicationResult(source);
          setApiResult(result);
          clearApplicationProgress(source.token);
          setPhase('completed');
        } catch (err) {
          setError(getApiErrorMessage(err));
        }
      }
      return;
    }

    setPhase('completed');
  }, [applicationHideResults, source]);

  const handleNext = useCallback(() => {
    const nextOrder =
      currentOrder < questions.length - 1
        ? currentOrder + 1
        : questions.findIndex((_, i) => !progress.answers[i]);

    if (nextOrder >= 0 && nextOrder < questions.length) {
      goToQuestion(nextOrder);
      return;
    }

    if (isTestFullyAnswered(questions.length, progress)) {
      void completeTest();
    }
  }, [completeTest, currentOrder, goToQuestion, progress, questions]);

  const finalizeAnswerProgress = useCallback(
    async (nextProgress: PlayerProgress) => {
      setProgress(nextProgress);
      persistProgress(source, nextProgress);
      setPhase('result');

      if (source.type === 'api' && isAuthenticatedApiSource(source)) {
        const allDone = isTestFullyAnswered(questions.length, nextProgress);
        if (allDone) {
          const result = await fetchApiResult(source);
          setApiResult(result);
        }
      } else if (source.type === 'application') {
        const allDone = isTestFullyAnswered(questions.length, nextProgress);
        if (allDone) {
          if (applicationHideResults) {
            setApplicationIsCompleted(true);
            clearApplicationProgress(source.token);
          } else {
            const result = await fetchApplicationResult(source);
            setApiResult(result);
            clearApplicationProgress(source.token);
          }
        }
      }
    },
    [applicationHideResults, questions.length, source],
  );

  const handleAnswer = useCallback(
    async (answerOrder: number) => {
      if (showingResult || phase === 'checking' || !currentQuestion) return;

      setSelectedOrder(answerOrder);
      setPhase('checking');
      setError(null);

      try {
        const submission = await submitPlayerAnswer({
          source,
          questionOrder: currentOrder,
          answerOrder,
          localDefinitions,
          applicationHideResults,
        });

        const nextProgress: PlayerProgress = {
          ...progress,
          answers: {
            ...progress.answers,
            [currentOrder]: {
              selectedAnswerOrder: answerOrder,
              correctAnswerOrder: submission.correctOrder,
              isCorrect: submission.isCorrect,
              ...(submission.explanation ? { explanation: submission.explanation } : {}),
            },
          },
          currentQuestionOrder: currentOrder,
          updatedAt: new Date().toISOString(),
        };

        await finalizeAnswerProgress(nextProgress);
      } catch (err) {
        if (canRestoreFromServerOnDuplicate(source) && isAlreadyAnsweredError(err)) {
          try {
            const restored = await restoreProgressFromServer(
              source,
              questions.length,
              currentOrder,
            );
            if (restored) {
              const existing = restored.answers[currentOrder];
              setProgress(restored);
              if (shouldPersistRestoredProgress(source)) {
                persistProgress(source, restored);
              }
              setSelectedOrder(existing.selectedAnswerOrder);
              setPhase('result');
              setError(null);
              return;
            }
          } catch {
            /* fall through to generic error */
          }
        }
        setError(getApiErrorMessage(err));
        setPhase('answering');
      }
    },
    [
      applicationHideResults,
      currentOrder,
      currentQuestion,
      finalizeAnswerProgress,
      localDefinitions,
      phase,
      progress,
      questions.length,
      showingResult,
      source,
    ],
  );

  const handleRetry = useCallback(async () => {
    await resetPlayerAttempt(source);
    const fresh = emptyProgress();
    setProgress(fresh);
    setApiResult(null);
    setHasNewQuestions(false);
    setSelectedOrder(null);
    setPhase('answering');
  }, [source]);

  const updateExplanationSettings = useCallback((patch: Partial<PlayerExplanationSettings>) => {
    setExplanationSettings((current) => {
      const next = { ...current, ...patch };
      savePlayerExplanationSettings(next);
      return next;
    });
  }, []);

  const activeRecord = showingResult ? currentRecord : null;
  const isChecking = phase === 'checking';
  const showNavOverlay = showingResult || isChecking;
  const canGoNext = showingResult;
  const canGoBack = currentOrder > 0;

  const rawExplanation =
    showingResult && activeRecord
      ? source.type === 'local' || isGuestApiSource(source)
        ? localDefinitions[currentOrder]?.explanation
        : activeRecord.explanation
      : undefined;

  const explanationText =
    showingResult && activeRecord && !applicationHideResults
      ? shouldShowExplanation(explanationSettings, activeRecord.isCorrect, rawExplanation)
        ? rawExplanation?.trim()
        : undefined
      : undefined;

  return {
    phase,
    testName,
    questions,
    progress,
    selectedOrder,
    error,
    hasNewQuestions,
    explanationSettings,
    applicationHideResults,
    currentOrder,
    currentQuestion,
    showingResult,
    completedSummary,
    canReviewAnswers,
    handleReviewAnswers,
    handleRetry,
    handleNext,
    handleAnswer,
    goToQuestion,
    updateExplanationSettings,
    activeRecord,
    isChecking,
    showNavOverlay,
    canGoNext,
    canGoBack,
    explanationText,
    canRetry: source.type !== 'application',
  };
}
