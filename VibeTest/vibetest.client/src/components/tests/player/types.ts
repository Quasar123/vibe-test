import type { PlayerProgress, PlayerQuestion } from '@/types/player';
import type { QuestionDefinition, TestResultResponse } from '@/types';

export type TestPlayerSource =
  | { type: 'local'; testId: string }
  | { type: 'api'; testId: number; authenticated?: boolean }
  | { type: 'application'; token: string; hideResults?: boolean; isCompleted?: boolean };

export interface TestPlayerProps {
  source: TestPlayerSource;
  onExit?: () => void;
}

export interface LoadedPlayerState {
  testName: string;
  questions: PlayerQuestion[];
  localDefinitions: QuestionDefinition[];
  progress: PlayerProgress;
  apiResult: TestResultResponse | null;
  applicationHideResults?: boolean;
  applicationIsCompleted?: boolean;
  refreshOptions?: {
    hideResults?: boolean;
    serverCompleted?: boolean;
  };
}

export interface AnswerSubmissionResult {
  correctOrder: number;
  isCorrect: boolean;
  explanation?: string;
}

export interface CompletedSummaryProps {
  testName: string;
  totalQuestions: number;
  correctAnswers: number;
  completedAt?: string;
  variant: 'full' | 'submitted';
}
