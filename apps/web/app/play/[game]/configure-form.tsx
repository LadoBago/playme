'use client';

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import {
  PlaymeClient,
  type SideSelectionMode,
  t,
  type I18nKey,
} from '@playme/shared';
import { browserApiBase } from '@/lib/api-base';

interface SideOption {
  id: string;
  label: string;
}

interface ConfigureFormProps {
  gameId: string;
  sides: readonly SideOption[];
  defaultHostSide: string;
}

const SIDE_MODE_OPTIONS: readonly { value: SideSelectionMode; labelKey: I18nKey }[] = [
  { value: 'hostPicksSpecific', labelKey: 'configure.sideMode.hostPicksSpecific' },
  { value: 'random', labelKey: 'configure.sideMode.random' },
  { value: 'challengerPicks', labelKey: 'configure.sideMode.challengerPicks' },
];

export function ConfigureForm({ gameId, sides, defaultHostSide }: ConfigureFormProps) {
  const router = useRouter();
  const [displayName, setDisplayName] = useState('');
  const [sideMode, setSideMode] = useState<SideSelectionMode>('hostPicksSpecific');
  const [hostSide, setHostSide] = useState<string>(defaultHostSide);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function submit() {
    setError(null);
    setSubmitting(true);

    const client = new PlaymeClient({ baseUrl: browserApiBase });
    const result = await client.createRoom({
      hostDisplayName: displayName.trim(),
      gameId,
      sideSelectionMode: sideMode,
      hostSide: sideMode === 'hostPicksSpecific' ? hostSide : undefined,
    });

    if (!result.ok) {
      setError(t(result.code as I18nKey));
      setSubmitting(false);
      return;
    }

    router.push(`/r/${result.value.code}`);
  }

  return (
    <form
      className="stack"
      onSubmit={(e) => {
        e.preventDefault();
        void submit();
      }}
    >
      <div>
        <label className="label" htmlFor="displayName">
          {t('configure.displayName.label')}
        </label>
        <input
          id="displayName"
          className="input"
          type="text"
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
          placeholder={t('configure.displayName.placeholder')}
          maxLength={24}
          required
          autoComplete="off"
        />
      </div>

      <div>
        <span className="label">{t('configure.sideMode.label')}</span>
        <div className="radio-group" role="radiogroup">
          {SIDE_MODE_OPTIONS.map((opt) => (
            <label
              key={opt.value}
              className={`radio-pill ${sideMode === opt.value ? 'radio-pill--active' : ''}`}
            >
              <input
                type="radio"
                name="sideMode"
                value={opt.value}
                checked={sideMode === opt.value}
                onChange={() => setSideMode(opt.value)}
              />
              {t(opt.labelKey)}
            </label>
          ))}
        </div>
      </div>

      {sideMode === 'hostPicksSpecific' ? (
        <div>
          <span className="label">{t('configure.hostSide.label')}</span>
          <div className="radio-group" role="radiogroup">
            {sides.map((s) => (
              <label
                key={s.id}
                className={`radio-pill ${hostSide === s.id ? 'radio-pill--active' : ''}`}
              >
                <input
                  type="radio"
                  name="hostSide"
                  value={s.id}
                  checked={hostSide === s.id}
                  onChange={() => setHostSide(s.id)}
                />
                {s.label}
              </label>
            ))}
          </div>
        </div>
      ) : null}

      {error ? <div className="banner banner--error">{error}</div> : null}

      <button type="submit" className="button-primary" disabled={submitting || !displayName.trim()}>
        {submitting ? t('configure.submitting') : t('configure.submit')}
      </button>
    </form>
  );
}
