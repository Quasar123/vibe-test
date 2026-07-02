import type { PlayerProgress, PlayerQuestion } from '@/types/player';

function dotClass(
  order: number,
  progress: PlayerProgress,
  current: number,
  hideResults = false,
): string {
  const record = progress.answers[order];
  const classes = ['vt-question-dot'];
  if (order === current) classes.push('vt-question-dot--current');
  if (!record) classes.push('vt-question-dot--unanswered');
  else if (hideResults) classes.push('vt-question-dot--answered');
  else if (record.isCorrect) classes.push('vt-question-dot--correct');
  else classes.push('vt-question-dot--incorrect');
  return classes.join(' ');
}

interface QuestionNavProps {
  questions: PlayerQuestion[];
  progress: PlayerProgress;
  currentOrder: number;
  hideResults?: boolean;
  onSelect: (order: number) => void;
}

export function QuestionNav({
  questions,
  progress,
  currentOrder,
  hideResults = false,
  onSelect,
}: QuestionNavProps) {
  return (
    <nav className="vt-question-nav" aria-label="Вопросы">
      {questions.map((q) => (
        <button
          key={q.order}
          type="button"
          className={dotClass(q.order, progress, currentOrder, hideResults)}
          onClick={() => onSelect(q.order)}
          title={`Вопрос ${q.order + 1}`}
        >
          {q.order + 1}
        </button>
      ))}
    </nav>
  );
}
