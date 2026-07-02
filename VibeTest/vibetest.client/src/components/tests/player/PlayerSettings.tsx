import type { PlayerExplanationSettings } from '@/utils/playerSettings';

interface PlayerSettingsProps {
  settings: PlayerExplanationSettings;
  onChange: (patch: Partial<PlayerExplanationSettings>) => void;
}

export function PlayerSettings({ settings, onChange }: PlayerSettingsProps) {
  return (
    <details className="vt-player-settings">
      <summary className="vt-player-settings__summary">Настройки пояснений</summary>
      <div className="vt-player-settings__body">
        <label className="vt-player-settings__label">
          <input
            type="checkbox"
            checked={settings.showOnCorrect}
            onChange={(e) => onChange({ showOnCorrect: e.target.checked })}
          />
          Показывать при правильном ответе
        </label>
        <label className="vt-player-settings__label">
          <input
            type="checkbox"
            checked={settings.showOnIncorrect}
            onChange={(e) => onChange({ showOnIncorrect: e.target.checked })}
          />
          Показывать при неправильном ответе
        </label>
      </div>
    </details>
  );
}
