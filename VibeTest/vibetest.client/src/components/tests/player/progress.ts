import type { AnsweredQuestionResponse } from '@/types';
import type { PlayerProgress } from '@/types/player';
import {
  getApiTestProgress,
  getApplicationProgress,
  getTestProgress,
  saveApiTestProgress,
  saveApplicationProgress,
  saveTestProgress,
} from '@/utils/storage';
import type { TestPlayerSource } from '@/components/tests/player/types';

export function emptyProgress(): PlayerProgress {
  return { answers: {}, currentQuestionOrder: 0, updatedAt: new Date().toISOString() };
}

export function isAuthenticatedApiSource(source: TestPlayerSource): boolean {
  return source.type === 'api' && source.authenticated !== false;
}

export function isGuestApiSource(source: TestPlayerSource): boolean {
  return source.type === 'api' && source.authenticated === false;
}

export function loadProgress(source: TestPlayerSource): PlayerProgress {
  if (source.type === 'local') {
    return getTestProgress(source.testId) ?? emptyProgress();
  }
  if (source.type === 'application') {
    return getApplicationProgress(source.token) ?? emptyProgress();
  }
  if (isGuestApiSource(source)) {
    return getApiTestProgress(source.testId) ?? emptyProgress();
  }
  return emptyProgress();
}

export function persistProgress(source: TestPlayerSource, progress: PlayerProgress): void {
  const payload = { ...progress, updatedAt: new Date().toISOString() };
  if (source.type === 'local') {
    saveTestProgress(source.testId, payload);
  } else if (source.type === 'application') {
    saveApplicationProgress(source.token, payload);
  } else if (isGuestApiSource(source)) {
    saveApiTestProgress(source.testId, payload);
  }
}

export function progressFromServerAnswers(
  answers: AnsweredQuestionResponse[],
  totalQuestions: number,
): PlayerProgress {
  const answerMap: PlayerProgress['answers'] = {};
  for (const answer of answers) {
    answerMap[answer.questionOrder] = {
      selectedAnswerOrder: answer.selectedAnswerOrder,
      correctAnswerOrder: answer.correctAnswerOrder,
      isCorrect: answer.isCorrect,
      ...(answer.explanation ? { explanation: answer.explanation } : {}),
    };
  }

  const firstUnanswered =
    Array.from({ length: totalQuestions }, (_, index) => index).find(
      (index) => !answerMap[index],
    ) ?? 0;

  return {
    answers: answerMap,
    currentQuestionOrder: firstUnanswered,
    updatedAt: new Date().toISOString(),
  };
}

export function resolveProgressFromSources(
  local: PlayerProgress,
  server: PlayerProgress,
): PlayerProgress {
  const localCount = Object.keys(local.answers).length;
  const serverCount = Object.keys(server.answers).length;
  if (localCount === 0 || serverCount > localCount) {
    return server;
  }
  return local;
}
