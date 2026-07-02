import type { PlayerPhase, PlayerQuestion, QuestionAnswerRecord } from '@/types/player';

interface QuestionCardProps {
  currentOrder: number;
  currentQuestion: PlayerQuestion;
  questionsCount: number;
  phase: PlayerPhase;
  showingResult: boolean;
  isChecking: boolean;
  showNavOverlay: boolean;
  selectedOrder: number | null;
  activeRecord: QuestionAnswerRecord | null;
  applicationHideResults: boolean;
  explanationText?: string;
  error: string | null;
  onAnswer: (answerOrder: number) => void;
  onNext: () => void;
}

export function QuestionCard({
  currentOrder,
  currentQuestion,
  questionsCount,
  phase,
  showingResult,
  isChecking,
  showNavOverlay,
  selectedOrder,
  activeRecord,
  applicationHideResults,
  explanationText,
  error,
  onAnswer,
  onNext,
}: QuestionCardProps) {
  return (
    <article className="vt-card">
      <p className="vt-muted">
        Вопрос {currentOrder + 1} из {questionsCount}
      </p>
      <h3>{currentQuestion.text}</h3>

      <div className="vt-options">
        {currentQuestion.answers.map((answer) => {
          const isSelected =
            (showingResult && activeRecord?.selectedAnswerOrder === answer.order) ||
            (!showingResult && selectedOrder === answer.order);
          const isCorrectOption =
            !applicationHideResults &&
            showingResult &&
            activeRecord?.correctAnswerOrder === answer.order;
          const isWrongSelected =
            !applicationHideResults &&
            showingResult &&
            isSelected &&
            activeRecord?.selectedAnswerOrder === answer.order &&
            !activeRecord.isCorrect;

          let optionClass = 'vt-option';
          if (applicationHideResults && isSelected && showingResult) {
            optionClass += ' vt-option--selected';
          } else {
            if (isCorrectOption) optionClass += ' vt-option--correct';
            if (isWrongSelected) optionClass += ' vt-option--incorrect';
          }

          return (
            <label key={answer.order} className={optionClass}>
              <input
                type="radio"
                name={`q-${currentOrder}`}
                checked={isSelected}
                disabled={showingResult || phase === 'checking'}
                onChange={() => onAnswer(answer.order)}
              />
              <span>{answer.text}</span>
            </label>
          );
        })}
      </div>

      {explanationText && <p className="vt-explanation">{explanationText}</p>}

      {error && <p className="vt-error">{error}</p>}

      {showNavOverlay && (
        <div
          className="vt-card__overlay"
          role="button"
          tabIndex={showingResult ? 0 : -1}
          aria-label="Следующий вопрос"
          aria-disabled={!showingResult || isChecking}
          onClick={() => {
            if (showingResult && !isChecking) onNext();
          }}
          onKeyDown={(e) => {
            if (showingResult && !isChecking && (e.key === 'Enter' || e.key === ' ')) {
              e.preventDefault();
              onNext();
            }
          }}
        />
      )}
    </article>
  );
}
