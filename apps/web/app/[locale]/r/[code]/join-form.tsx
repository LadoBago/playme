'use client';

import { useEffect, useState } from 'react';
import { type I18nKey, type PlaymeClient, type RoomDto } from '@playme/shared';
import { readStoredDisplayName, writeStoredDisplayName } from '@/features/preferences';
import { writeActiveRoom } from '@/features/resume';
import { track } from '@/lib/analytics';
import { useTranslator } from '@/lib/use-locale';

interface JoinFormProps {
  room: RoomDto;
  sides: readonly { id: string; labelKey: string }[];
  client: PlaymeClient;
  onJoined: (room: RoomDto) => void;
}

export function JoinForm({ room, sides, client, onJoined }: JoinFormProps) {
  const { t } = useTranslator();
  const [displayName, setDisplayName] = useState('');
  const [side, setSide] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const sidePickRequired = room.sideSelectionMode === 'challengerPicks';

  // Prefill the name remembered from a previous room (after mount — this
  // is a client component but the room page is still server-rendered).
  useEffect(() => {
    const stored = readStoredDisplayName();
    if (stored) setDisplayName(stored);
  }, []);

  async function submit() {
    setError(null);
    setSubmitting(true);

    const result = await client.joinRoom(room.code, {
      displayName: displayName.trim(),
      side: sidePickRequired ? side : undefined,
    });

    if (!result.ok) {
      setError(t(result.code as I18nKey));
      setSubmitting(false);
      return;
    }

    writeStoredDisplayName(displayName);
    // Mirror of the session cookie just minted (see configure-form.tsx).
    writeActiveRoom({
      code: result.value.code,
      gameId: result.value.gameId,
      role: 'challenger',
      savedAt: Date.now(),
    });
    track({ name: 'room_joined', props: { gameId: result.value.gameId } });
    onJoined(result.value);
  }

  const submittable = displayName.trim().length > 0 && (!sidePickRequired || side !== '');

  return (
    <div className="card stack">
      <h2 style={{ fontSize: '1.2rem' }}>{t('join.title')}</h2>
      <form
        className="stack"
        onSubmit={(e) => {
          e.preventDefault();
          void submit();
        }}
      >
        <div>
          <label className="label" htmlFor="joinDisplayName">
            {t('join.displayName.label')}
          </label>
          <input
            id="joinDisplayName"
            className="input"
            type="text"
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            placeholder={t('join.displayName.placeholder')}
            maxLength={24}
            required
            autoComplete="off"
          />
        </div>

        {sidePickRequired ? (
          <div>
            <span className="label">{t('join.side.label')}</span>
            <div className="radio-group" role="radiogroup">
              {sides.map((s) => (
                <label key={s.id} className={`radio-pill ${side === s.id ? 'radio-pill--active' : ''}`}>
                  <input
                    type="radio"
                    name="joinSide"
                    value={s.id}
                    checked={side === s.id}
                    onChange={() => setSide(s.id)}
                  />
                  {t(s.labelKey as I18nKey)}
                </label>
              ))}
            </div>
          </div>
        ) : null}

        {error ? <div className="banner banner--error">{error}</div> : null}

        <button
          type="submit"
          className="button-primary"
          disabled={!submittable || submitting}
          aria-busy={submitting}
        >
          {submitting ? (
            <>
              <span className="button-spinner" aria-hidden="true" />
              {t('join.submitting')}
            </>
          ) : (
            t('join.submit')
          )}
        </button>
      </form>
    </div>
  );
}
