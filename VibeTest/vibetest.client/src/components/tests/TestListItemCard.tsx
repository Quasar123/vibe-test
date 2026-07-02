import type { ReactNode } from 'react';
import { TestDifficultyBadge } from '@/components/tests/TestDifficultyBadge';
import type { TestDifficulty } from '@/types';

type TestListItemCardProps = {
  listClassName: 'full-list' | 'guest-list';
  title: string;
  difficulty: TestDifficulty;
  description?: string;
  badges?: ReactNode;
  meta: ReactNode;
  actions: ReactNode;
};

export function TestListItemCard({
  listClassName,
  title,
  difficulty,
  description,
  badges,
  meta,
  actions,
}: TestListItemCardProps) {
  const prefix = listClassName;
  const isGuest = listClassName === 'guest-list';

  const content = (
    <>
      <div
        className={`${prefix}__title`}
        style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}
      >
        {title}
        <TestDifficultyBadge difficulty={difficulty} />
        {badges}
      </div>
      {description && (
        <details className="test-list-card__details">
          <summary className="test-list-card__details-summary">Описание</summary>
          <div className="test-list-card__details-body">
            <p className="test-list-card__description">{description}</p>
          </div>
        </details>
      )}
      {meta}
    </>
  );

  const actionsBlock = (
    <div className={isGuest ? 'guest-list__actions' : 'test-list-card__actions'}>{actions}</div>
  );

  if (isGuest) {
    return (
      <li className={`${prefix}__item`}>
        <div>{content}</div>
        {actionsBlock}
      </li>
    );
  }

  return (
    <li className={`${prefix}__item`}>
      {content}
      {actionsBlock}
    </li>
  );
}
