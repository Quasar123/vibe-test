import { applicationsApi, testsApi } from '@/full/api';
import { getApiErrorMessage } from '@/full/context/AuthContext';
import type { QuestionDefinition } from '@/types';
import type { PlayerProgress } from '@/types/player';
import { getCorrectAnswerOrder } from '@/utils/playerHelpers';
import { clearApplicationProgress, clearApiTestProgress, clearTestProgress, getLocalTestById } from '@/utils/storage';
import {
  definitionsFromPublicPlay,
  playerQuestionsFromDefinitions,
  questionsFromDetailDtos,
} from '@/components/tests/player/mappers';
import {
  emptyProgress,
  isAuthenticatedApiSource,
  isGuestApiSource,
  loadProgress,
  persistProgress,
  progressFromServerAnswers,
  resolveProgressFromSources,
} from '@/components/tests/player/progress';
import type {
  AnswerSubmissionResult,
  LoadedPlayerState,
  TestPlayerSource,
} from '@/components/tests/player/types';

export const ALREADY_ANSWERED_MESSAGE = 'На этот вопрос уже дан ответ';

export function isAlreadyAnsweredError(err: unknown): boolean {
  return getApiErrorMessage(err) === ALREADY_ANSWERED_MESSAGE;
}

export async function loadPlayerState(source: TestPlayerSource): Promise<LoadedPlayerState> {
  if (source.type === 'local') {
    const test = getLocalTestById(source.testId);
    if (!test) {
      throw new Error('Тест не найден');
    }

    const questions = playerQuestionsFromDefinitions(test.questions);
    const progress = loadProgress(source);

    return {
      testName: test.name,
      questions,
      localDefinitions: test.questions,
      progress,
      apiResult: null,
    };
  }

  if (source.type === 'application') {
    const detail = await applicationsApi.getDetail(source.token);
    const hideResults = detail.hideResultsFromParticipant;
    const serverCompleted = detail.isCompleted;
    const [result, serverAnswers] = await Promise.all([
      hideResults
        ? Promise.resolve(null)
        : applicationsApi.getResult(source.token).catch(() => null),
      serverCompleted
        ? Promise.resolve({ answers: [] })
        : applicationsApi.getAnswers(source.token).catch(() => ({ answers: [] })),
    ]);

    const questions = questionsFromDetailDtos(detail.questions);
    const localProg = serverCompleted ? emptyProgress() : loadProgress(source);
    if (serverCompleted) {
      clearApplicationProgress(source.token);
    }
    const serverProg = progressFromServerAnswers(serverAnswers.answers, questions.length);
    const progress = serverCompleted
      ? localProg
      : resolveProgressFromSources(localProg, serverProg);
    if (!serverCompleted && progress !== localProg) {
      persistProgress(source, progress);
    }

    return {
      testName: detail.name,
      questions,
      localDefinitions: [],
      progress,
      apiResult: result,
      applicationHideResults: hideResults,
      applicationIsCompleted: serverCompleted,
      refreshOptions: { hideResults, serverCompleted },
    };
  }

  if (isGuestApiSource(source)) {
    const full = await testsApi.getPublicPlay(source.testId);
    const definitions = definitionsFromPublicPlay(full);
    const questions = playerQuestionsFromDefinitions(definitions);
    const progress = loadProgress(source);

    return {
      testName: full.name,
      questions,
      localDefinitions: definitions,
      progress,
      apiResult: null,
    };
  }

  const [detail, result, serverAnswers] = await Promise.all([
    testsApi.getDetail(source.testId),
    testsApi.getResult(source.testId).catch(() => null),
    testsApi.getAnswers(source.testId).catch(() => ({ answers: [] })),
  ]);

  const questions = questionsFromDetailDtos(detail.questions);
  const progress = progressFromServerAnswers(serverAnswers.answers, questions.length);

  return {
    testName: detail.name,
    questions,
    localDefinitions: [],
    progress,
    apiResult: result,
  };
}

export interface SubmitAnswerContext {
  source: TestPlayerSource;
  questionOrder: number;
  answerOrder: number;
  localDefinitions: QuestionDefinition[];
  applicationHideResults: boolean;
}

export async function submitPlayerAnswer(
  context: SubmitAnswerContext,
): Promise<AnswerSubmissionResult> {
  const { source, questionOrder, answerOrder, localDefinitions, applicationHideResults } =
    context;

  if (source.type === 'local' || isGuestApiSource(source)) {
    const def = localDefinitions[questionOrder];
    const correctOrder = getCorrectAnswerOrder(def);
    return {
      correctOrder,
      isCorrect: answerOrder === correctOrder,
      explanation: def.explanation,
    };
  }

  if (source.type === 'application') {
    const response = await applicationsApi.submitAnswer(source.token, {
      questionOrder,
      selectedAnswerOrder: answerOrder,
    });
    if (applicationHideResults) {
      return {
        correctOrder: answerOrder,
        isCorrect: true,
        explanation: undefined,
      };
    }
    return {
      correctOrder: response.correctAnswerOrder,
      isCorrect: answerOrder === response.correctAnswerOrder,
      explanation: response.explanation,
    };
  }

  const response = await testsApi.submitAnswer(source.testId, {
    questionOrder,
    selectedAnswerOrder: answerOrder,
  });
  return {
    correctOrder: response.correctAnswerOrder,
    isCorrect: answerOrder === response.correctAnswerOrder,
    explanation: response.explanation,
  };
}

export async function fetchApiResult(source: TestPlayerSource) {
  if (source.type !== 'api' || isGuestApiSource(source)) {
    return null;
  }
  return testsApi.getResult(source.testId);
}

export async function fetchApplicationResult(source: TestPlayerSource & { type: 'application' }) {
  return applicationsApi.getResult(source.token);
}

export async function restoreProgressFromServer(
  source: TestPlayerSource,
  totalQuestions: number,
  currentOrder: number,
): Promise<PlayerProgress | null> {
  if (source.type !== 'api' && source.type !== 'application') {
    return null;
  }

  const serverAnswers =
    source.type === 'api'
      ? await testsApi.getAnswers(source.testId)
      : await applicationsApi.getAnswers(source.token);
  const restored = progressFromServerAnswers(serverAnswers.answers, totalQuestions);
  const existing = restored.answers[currentOrder];
  if (!existing) {
    return null;
  }
  return {
    ...restored,
    currentQuestionOrder: currentOrder,
    updatedAt: new Date().toISOString(),
  };
}

export async function resetPlayerAttempt(source: TestPlayerSource): Promise<void> {
  if (source.type === 'application') {
    return;
  }

  if (source.type === 'local') {
    clearTestProgress(source.testId);
    return;
  }

  if (isGuestApiSource(source)) {
    clearApiTestProgress(source.testId);
    return;
  }

  try {
    await testsApi.deleteResult(source.testId);
  } catch {
    /* ignore if no result */
  }
}

export function shouldPersistRestoredProgress(source: TestPlayerSource): boolean {
  return source.type === 'application';
}

export function canRestoreFromServerOnDuplicate(source: TestPlayerSource): boolean {
  return (
    (source.type === 'api' && isAuthenticatedApiSource(source)) || source.type === 'application'
  );
}
