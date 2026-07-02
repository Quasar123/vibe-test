import type { QuestionDefinition, TestFullResponse } from '@/types';
import type { QuestionDetailDto } from '@/types/api';
import type { PlayerQuestion } from '@/types/player';
import { toPlayerQuestions } from '@/utils/playerHelpers';

export function definitionsFromPublicPlay(full: TestFullResponse): QuestionDefinition[] {
  return [...full.questions]
    .sort((a, b) => a.order - b.order)
    .map((question) => ({
      text: question.text,
      answers: question.answers,
      correct: question.correct,
      ...(question.explanation ? { explanation: question.explanation } : {}),
    }));
}

export function questionsFromDetailDtos(questions: QuestionDetailDto[]): PlayerQuestion[] {
  const sortedQuestions = [...questions].sort((a, b) => a.order - b.order);
  return sortedQuestions.map((q, index) => ({
    order: index,
    text: q.text,
    answers: [...q.answers]
      .sort((a, b) => a.order - b.order)
      .map((a, answerIndex) => ({ order: answerIndex, text: a.text })),
  }));
}

export function playerQuestionsFromDefinitions(definitions: QuestionDefinition[]): PlayerQuestion[] {
  return toPlayerQuestions(definitions);
}
