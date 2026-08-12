import type { TestDifficulty } from '@/types';

export const TEST_DIFFICULTIES: TestDifficulty[] = ['easy', 'medium', 'hard'];

export const TEST_DIFFICULTY_LABELS: Record<TestDifficulty, string> = {
  easy: 'Лёгкий',
  medium: 'Средний',
  hard: 'Сложный',
};

export function normalizeDifficulty(value: unknown): TestDifficulty {
  if (value === 'medium' || value === 'hard') {
    return value;
  }
  return 'easy';
}

export function parseDifficulty(value: unknown): TestDifficulty {
  if (value === 'easy' || value === 'medium' || value === 'hard') {
    return value;
  }
  return 'easy';
}
