'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useState } from 'react';
import {
  PlaymeClient,
  type BotLevel,
  type PlayerKind,
  type SideSelectionMode,
  type I18nKey,
  localizedHref,
} from '@playme/shared';
import { browserApiBase } from '@/lib/api-base';
import { track } from '@/lib/analytics';
import { botLevelKey } from '@/lib/player-label';
import { useTranslator } from '@/lib/use-locale';

interface SideOption {
  id: string;
  label: string;
}

interface ConfigureFormProps {
  gameId: string;
  sides: readonly SideOption[];
  defaultHostSide: string;
  /**
   * Whether the server's module for this game can play one side itself
   * (Sprint 12 seam D). Read from `GET /api/games` by the page; the
   * opponent toggle only renders when true, so a game without a bot
   * shows exactly the form it showed before.
   */
  supportsBot: boolean;
}

const SIDE_MODE_OPTIONS: readonly { value: SideSelectionMode; labelKey: I18nKey }[] = [
  { value: 'hostPicksSpecific', labelKey: 'configure.sideMode.hostPicksSpecific' },
  { value: 'random', labelKey: 'configure.sideMode.random' },
  { value: 'challengerPicks', labelKey: 'configure.sideMode.challengerPicks' },
];

const OPPONENT_OPTIONS: readonly { value: PlayerKind; labelKey: I18nKey }[] = [
  { value: 'human', labelKey: 'configure.opponent.human' },
  { value: 'bot', labelKey: 'configure.opponent.bot' },
];

const BOT_LEVELS: readonly BotLevel[] = ['easy', 'medium', 'hard'];

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

export function ConfigureForm({ gameId, sides, defaultHostSide, supportsBot }: ConfigureFormProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t, locale } = useTranslator();
  const [displayName, setDisplayName] = useState('');
  const [opponent, setOpponent] = useState<PlayerKind>('human');
  const [botLevel, setBotLevel] = useState<BotLevel>('medium');
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
  const isBotRoom = supportsBot && opponent === 'bot';
  // "Let my friend pick" has no meaning without a friend; the server
  // rejects it for a bot room, so the option is hidden rather than
  // surfacing as an error after submit.
  const sideModeOptions = isBotRoom
    ? SIDE_MODE_OPTIONS.filter((o) => o.value !== 'challengerPicks')
    : SIDE_MODE_OPTIONS;

  function chooseOpponent(kind: PlayerKind) {
    setOpponent(kind);
    if (kind === 'bot' && sideMode === 'challengerPicks') {
      setSideMode('hostPicksSpecific');
    }
  }

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
      opponent: isBotRoom ? 'bot' : undefined,
      botLevel: isBotRoom ? botLevel : undefined,
    });

    if (!result.ok) {
      setError(t(result.code as I18nKey));
      setSubmitting(false);
      return;
    }

    track({ name: 'room_created', props: { gameId, opponent: isBotRoom ? 'bot' : 'human' } });
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

      {supportsBot ? (
        <div>
          <span className="label">{t('configure.opponent.label')}</span>
          <div className="radio-group" role="radiogroup">
            {OPPONENT_OPTIONS.map((opt) => (
              <label
                key={opt.value}
                className={`radio-pill ${opponent === opt.value ? 'radio-pill--active' : ''}`}
              >
                <input
                  type="radio"
                  name="opponent"
                  value={opt.value}
                  checked={opponent === opt.value}
                  onChange={() => chooseOpponent(opt.value)}
                />
                {t(opt.labelKey)}
              </label>
            ))}
          </div>
        </div>
      ) : null}

      {isBotRoom ? (
        <div>
          <span className="label">{t('configure.botLevel.label')}</span>
          <div className="radio-group" role="radiogroup">
            {BOT_LEVELS.map((level) => (
              <label
                key={level}
                className={`radio-pill ${botLevel === level ? 'radio-pill--active' : ''}`}
              >
                <input
                  type="radio"
                  name="botLevel"
                  value={level}
                  checked={botLevel === level}
                  onChange={() => setBotLevel(level)}
                />
                {t(botLevelKey(level))}
              </label>
            ))}
          </div>
        </div>
      ) : null}

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
          {sideModeOptions.map((opt) => (
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
          t(isBotRoom ? 'configure.submitBot' : 'configure.submit')
        )}
      </button>
    </form>
  );
}
