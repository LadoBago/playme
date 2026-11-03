'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useId, useRef, useState } from 'react';
import {
  PlaymeClient,
  type BotLevel,
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
   * (Sprint 12 seam D). Read from `GET /api/games` by the page. When true
   * the card opens with two tabs — play with a friend / play with a bot;
   * when false there are no tabs and the form is the friend form alone.
   */
  supportsBot: boolean;
}

/** Which complete form the card shows. URL state: `?vs=bot` selects the bot tab. */
type Opponent = 'friend' | 'bot';

const VS_PARAM = 'vs';

const SIDE_MODE_OPTIONS: readonly { value: SideSelectionMode; labelKey: I18nKey }[] = [
  { value: 'hostPicksSpecific', labelKey: 'configure.sideMode.hostPicksSpecific' },
  { value: 'random', labelKey: 'configure.sideMode.random' },
  { value: 'challengerPicks', labelKey: 'configure.sideMode.challengerPicks' },
];

const TABS: readonly { value: Opponent; labelKey: I18nKey }[] = [
  { value: 'friend', labelKey: 'configure.tab.friend' },
  { value: 'bot', labelKey: 'configure.tab.bot' },
];

const BOT_LEVELS: readonly BotLevel[] = ['easy', 'medium', 'hard'];

/** Sentinel for the bot tab's "Your side" group; never a real side id. */
const RANDOM_SIDE = '__random';

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
  const tabsId = useId();
  const tabRefs = useRef(new Map<Opponent, HTMLButtonElement>());

  // Friend tab only — the bot tab has no name field (nobody else ever reads
  // it). Kept across tab switches so a round trip through the bot tab does
  // not lose what was typed.
  const [displayName, setDisplayName] = useState('');
  const [opponent, setOpponent] = useState<Opponent>(() =>
    supportsBot && searchParams?.get(VS_PARAM) === 'bot' ? 'bot' : 'friend',
  );
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
  // The bot tab has no "how to assign sides" question: there is no friend
  // to let pick, so "Your side" simply offers the two sides plus Random,
  // mapped onto the same two server modes the friend tab uses.
  const botSideChoice: string = sideMode === 'random' ? RANDOM_SIDE : hostSide;

  function chooseBotSide(choice: string) {
    if (choice === RANDOM_SIDE) {
      setSideMode('random');
    } else {
      setSideMode('hostPicksSpecific');
      setHostSide(choice);
    }
  }

  function selectTab(next: Opponent) {
    setOpponent(next);
    setError(null);
    if (next === 'bot' && sideMode === 'challengerPicks') {
      setSideMode('hostPicksSpecific');
    }
    // Keep the choice in the URL so a reload or a shared link lands on the
    // same tab; replace, not push — tab switches are not navigation.
    const url = new URL(window.location.href);
    if (next === 'bot') url.searchParams.set(VS_PARAM, 'bot');
    else url.searchParams.delete(VS_PARAM);
    window.history.replaceState(window.history.state, '', url);
  }

  function onTabKeyDown(e: React.KeyboardEvent<HTMLButtonElement>, index: number) {
    if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return;
    e.preventDefault();
    const delta = e.key === 'ArrowRight' ? 1 : -1;
    const nextTab = TABS.at((index + delta + TABS.length) % TABS.length);
    if (!nextTab) return;
    selectTab(nextTab.value);
    tabRefs.current.get(nextTab.value)?.focus();
  }

  async function submit() {
    setError(null);
    setSubmitting(true);

    // The bot tab asks for no name — nobody else ever reads it — but the
    // API contract still requires one, so it submits the localized default.
    const name = isBotRoom ? t('configure.displayName.default') : displayName.trim();
    const client = new PlaymeClient({ baseUrl: browserApiBase });
    const result = await client.createRoom({
      hostDisplayName: name,
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

  const panelId = `${tabsId}-panel`;

  return (
    <div>
      {supportsBot ? (
        <div className="tabs" role="tablist" aria-label={t('configure.title')}>
          {TABS.map((tab, index) => {
            const active = opponent === tab.value;
            return (
              <button
                key={tab.value}
                ref={(el) => {
                  if (el) tabRefs.current.set(tab.value, el);
                  else tabRefs.current.delete(tab.value);
                }}
                type="button"
                role="tab"
                id={`${tabsId}-tab-${tab.value}`}
                aria-selected={active}
                aria-controls={panelId}
                tabIndex={active ? 0 : -1}
                className={`tabs__tab ${active ? 'tabs__tab--active' : ''}`}
                onClick={() => selectTab(tab.value)}
                onKeyDown={(e) => onTabKeyDown(e, index)}
              >
                {t(tab.labelKey)}
              </button>
            );
          })}
        </div>
      ) : null}

      <form
        id={panelId}
        role={supportsBot ? 'tabpanel' : undefined}
        aria-labelledby={supportsBot ? `${tabsId}-tab-${opponent}` : undefined}
        className="stack"
        onSubmit={(e) => {
          e.preventDefault();
          void submit();
        }}
      >
        {isBotRoom ? null : (
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
        )}

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

        {isBotRoom ? (
          <div>
            <span className="label">{t('configure.hostSide.label')}</span>
            <div className="radio-group" role="radiogroup">
              {[...sides.map((s) => ({ id: s.id, label: s.label })), { id: RANDOM_SIDE, label: t('configure.sideMode.random') }].map((s) => (
                <label
                  key={s.id}
                  className={`radio-pill ${botSideChoice === s.id ? 'radio-pill--active' : ''}`}
                >
                  <input
                    type="radio"
                    name="botSide"
                    value={s.id}
                    checked={botSideChoice === s.id}
                    onChange={() => chooseBotSide(s.id)}
                  />
                  {s.label}
                </label>
              ))}
            </div>
          </div>
        ) : (
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
        )}

        {!isBotRoom && sideMode === 'hostPicksSpecific' ? (
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

        {supportsBot && !isBotRoom ? (
          <p style={{ color: 'var(--fg-muted)', margin: 0, fontSize: '0.9rem' }}>
            {t('configure.friend.hint')}
          </p>
        ) : null}

        {error ? <div className="banner banner--error">{error}</div> : null}

        <button
          type="submit"
          className="button-primary"
          disabled={submitting || (!isBotRoom && !displayName.trim())}
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
    </div>
  );
}
