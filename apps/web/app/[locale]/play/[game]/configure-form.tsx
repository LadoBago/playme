'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useState } from 'react';
import {
  PlaymeClient,
  type SideSelectionMode,
  type I18nKey,
  localizedHref,
} from '@playme/shared';
import { browserApiBase } from '@/lib/api-base';
import { track } from '@/lib/analytics';
import { useTranslator } from '@/lib/use-locale';

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

/**
 * Allowed board sizes for the unified Tic-Tac-Toe module (Sprint 9 PR1b).
 * Matches the server-side `TicTacToeGameModule.AllowedBoardSizes`. The
 * picker only appears for `gameId === 'tictactoe'`.
 */
const TICTACTOE_BOARD_SIZES = [3, 6, 9] as const;
type TicTacToeBoardSize = (typeof TICTACTOE_BOARD_SIZES)[number];

function parseBoardSizeParam(value: string | null): TicTacToeBoardSize {
  if (value === '3') return 3;
  if (value === '6') return 6;
  if (value === '9') return 9;
  return 3;
}

export function ConfigureForm({ gameId, sides, defaultHostSide }: ConfigureFormProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t, locale } = useTranslator();
  const [displayName, setDisplayName] = useState('');
  const [sideMode, setSideMode] = useState<SideSelectionMode>('hostPicksSpecific');
  const [hostSide, setHostSide] = useState<string>(defaultHostSide);
  // Initial value reads from `?size=` so the 301 redirect from the legacy
  // `/play/tictactoe-{3x3,6x6,9x9}` URLs pre-selects the matching size.
  const [boardSize, setBoardSize] = useState<TicTacToeBoardSize>(() =>
    parseBoardSizeParam(searchParams?.get('size') ?? null),
  );
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const isUnifiedTicTacToe = gameId === 'tictactoe';

  async function submit() {
    setError(null);
    setSubmitting(true);

    const client = new PlaymeClient({ baseUrl: browserApiBase });
    const result = await client.createRoom({
      hostDisplayName: displayName.trim(),
      gameId,
      sideSelectionMode: sideMode,
      hostSide: sideMode === 'hostPicksSpecific' ? hostSide : undefined,
      gameOptions: isUnifiedTicTacToe ? { boardSize } : undefined,
    });

    if (!result.ok) {
      setError(t(result.code as I18nKey));
      setSubmitting(false);
      return;
    }

    track({ name: 'room_created', props: { gameId } });
    router.push(localizedHref(`/r/${result.value.code}`, locale));
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

      {isUnifiedTicTacToe ? (
        <div>
          <span className="label">{t('configure.boardSize.label')}</span>
          <div className="radio-group" role="radiogroup">
            {TICTACTOE_BOARD_SIZES.map((size) => (
              <label
                key={size}
                className={`radio-pill ${boardSize === size ? 'radio-pill--active' : ''}`}
              >
                <input
                  type="radio"
                  name="boardSize"
                  value={size}
                  checked={boardSize === size}
                  onChange={() => setBoardSize(size)}
                />
                {`${size}×${size}`}
              </label>
            ))}
          </div>
        </div>
      ) : null}

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

      <button
        type="submit"
        className="button-primary"
        disabled={submitting || !displayName.trim()}
        aria-busy={submitting}
      >
        {submitting ? (
          <>
            <span className="button-spinner" aria-hidden="true" />
            {t('configure.submitting')}
          </>
        ) : (
          t('configure.submit')
        )}
      </button>
    </form>
  );
}
