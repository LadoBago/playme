'use client';

import { useMemo, useRef, useState } from 'react';
import { t as translate, type Locale } from '@playme/shared';
import { useTranslator } from '@/lib/use-locale';
import type { GameView, GameViewProps } from '../types';
import { generateFleet } from './fleet';
import {
  GRID_SIZE,
  SeaBattleFullStateSchema,
  SeaBattleProjectionSchema,
  cellKey,
  shipCells,
  type Ship,
  type ShotResult,
  type ShotView,
} from './schema';

/**
 * Localised short side label ("First" / "Second") for the platform's
 * player card. Side vocab stays inside this module (CLAUDE.md §7
 * "Platform thinness").
 */
export function seabattleSideLabel(side: string, locale: Locale): string | null {
  if (side === 'first') return translate('games.seabattle.shortSideFirst', locale);
  if (side === 'second') return translate('games.seabattle.shortSideSecond', locale);
  return null;
}

/**
 * Everything the boards need, normalized from the two wire shapes
 * (docs/games/seabattle.md): the live per-viewer projection while the
 * match runs, and the full reveal once it's terminal. Hidden information
 * never reaches this code pre-terminal — the server projects.
 */
interface ViewModel {
  readonly phase: 'setup' | 'battle';
  readonly myFleet: readonly Ship[] | null;
  /** Opponent ships visible to me: sunk-by-me live, the whole fleet at terminal. */
  readonly revealedOpponentShips: readonly Ship[];
  readonly myShots: readonly ShotView[];
  readonly shotsAtMe: readonly ShotView[];
}

/** Derive shot results client-side for the terminal full-reveal shape. */
function deriveShotViews(
  shots: readonly { x: number; y: number }[],
  defendingFleet: readonly Ship[] | undefined,
): ShotView[] {
  const shotSet = new Set(shots.map((s) => cellKey(s.x, s.y)));
  return shots.map((shot) => {
    let result: ShotResult = 'miss';
    for (const ship of defendingFleet ?? []) {
      const cells = shipCells(ship);
      if (!cells.some((c) => c.x === shot.x && c.y === shot.y)) continue;
      result = cells.every((c) => shotSet.has(cellKey(c.x, c.y))) ? 'sunk' : 'hit';
      break;
    }
    return { ...shot, result };
  });
}

function parseViewModel(matchState: string, callerSide: string | null): ViewModel | null {
  let raw: unknown;
  try {
    raw = JSON.parse(matchState);
  } catch {
    return null;
  }

  const mySide = callerSide === 'second' ? 'second' : 'first';

  const projection = SeaBattleProjectionSchema.safeParse(raw);
  if (projection.success) {
    const p = projection.data;
    const opp = mySide === 'first' ? 'second' : 'first';
    return {
      phase: p.phase,
      myFleet: p.yourFleet ?? null,
      revealedOpponentShips: p.sunk[mySide],
      myShots: p.shots[mySide],
      shotsAtMe: p.shots[opp],
    };
  }

  const full = SeaBattleFullStateSchema.safeParse(raw);
  if (!full.success) return null;
  const f = full.data;
  const myFleet = mySide === 'first' ? f.firstFleet : f.secondFleet;
  const oppFleet = mySide === 'first' ? f.secondFleet : f.firstFleet;
  const myRawShots = (mySide === 'first' ? f.shotsByFirst : f.shotsBySecond) ?? [];
  const oppRawShots = (mySide === 'first' ? f.shotsBySecond : f.shotsByFirst) ?? [];
  return {
    phase: 'battle',
    myFleet: myFleet ?? null,
    revealedOpponentShips: oppFleet ?? [],
    myShots: deriveShotViews(myRawShots, oppFleet),
    shotsAtMe: deriveShotViews(oppRawShots, myFleet),
  };
}

/** Cells guaranteed empty by the no-touch rule around ships the viewer
 *  has sunk — rendered dimmed as a QoL hint; still legal targets. */
function deducedEmptyCells(sunkShips: readonly Ship[]): Set<number> {
  const cells = new Set<number>();
  for (const ship of sunkShips) {
    for (const cell of shipCells(ship)) {
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const x = cell.x + dx;
          const y = cell.y + dy;
          if (x >= 0 && x < GRID_SIZE && y >= 0 && y < GRID_SIZE) {
            cells.add(cellKey(x, y));
          }
        }
      }
    }
    for (const cell of shipCells(ship)) {
      cells.delete(cellKey(cell.x, cell.y));
    }
  }
  return cells;
}

export const SeaBattleView: GameView = ({
  matchState,
  callerSide,
  canPlay,
  matchEnded,
  onSubmitMove,
  setup,
  onSubmitSetup,
}: GameViewProps) => {
  const { t, tf } = useTranslator();
  const model = useMemo(() => parseViewModel(matchState, callerSide), [matchState, callerSide]);

  // Setup-phase local state: the draft fleet lives only in this client
  // until the single commit (docs/games/seabattle.md "Setup phase").
  const [draftFleet, setDraftFleet] = useState<Ship[]>(() => generateFleet());

  // Visual pending state for the commit button. There's no explicit
  // "commit failed" signal back into the view (errors surface in the
  // platform banner), so pending self-clears after a short window — the
  // success path replaces this whole branch via setup.mineCommitted.
  const [commitPending, setCommitPending] = useState(false);
  const commitFleet = () => {
    if (commitPending) return;
    setCommitPending(true);
    onSubmitSetup?.({ ships: draftFleet });
    window.setTimeout(() => setCommitPending(false), 4000);
  };

  // Narrow-screen board pager: the two grids form a scroll-snap carousel
  // (one board per swipe); these track which slide is in view so the tab
  // pills reflect it. On wide containers the boards sit side by side and
  // the pills are hidden — the scroll state simply never changes.
  const boardsRef = useRef<HTMLDivElement | null>(null);
  const [activeBoard, setActiveBoard] = useState<'target' | 'fleet'>('target');
  const onBoardsScroll = () => {
    const el = boardsRef.current;
    if (!el) return;
    setActiveBoard(el.scrollLeft > el.clientWidth / 2 ? 'fleet' : 'target');
  };
  const scrollToBoard = (board: 'target' | 'fleet') => {
    const el = boardsRef.current;
    if (!el) return;
    el.scrollTo({ left: board === 'target' ? 0 : el.scrollWidth, behavior: 'smooth' });
  };

  if (!model) {
    return <p className="banner banner--error">{t('errors.unknown')}</p>;
  }

  if (model.phase === 'setup') {
    const mineCommitted = setup?.mineCommitted ?? model.myFleet != null;
    const opponentCommitted = setup?.opponentCommitted ?? false;
    return (
      <div className="sb stack" aria-label={t('games.seabattle.setup.title')}>
        <h2 className="sb__heading">{t('games.seabattle.setup.title')}</h2>
        <p className="sb__hint">{t('games.seabattle.setup.hint')}</p>
        <FleetGrid
          fleet={mineCommitted ? (model.myFleet ?? draftFleet) : draftFleet}
          shotsAtMe={[]}
          label={t('games.seabattle.board.yours')}
          t={{ t, tf }}
        />
        {mineCommitted ? (
          <p className="sb__status" role="status">
            {t('games.seabattle.setup.committed')}
          </p>
        ) : (
          <div className="sb__setup-controls">
            <button
              type="button"
              className="button-ghost"
              disabled={commitPending}
              onClick={() => setDraftFleet(generateFleet())}
            >
              {t('games.seabattle.setup.reroll')}
            </button>
            <button
              type="button"
              className="button-primary"
              disabled={commitPending}
              onClick={commitFleet}
            >
              {commitPending
                ? t('games.seabattle.setup.committing')
                : t('games.seabattle.setup.commit')}
            </button>
          </div>
        )}
        <p className="sb__status sb__status--muted" role="status">
          {opponentCommitted
            ? t('games.seabattle.setup.opponentReady')
            : t('games.seabattle.setup.opponentPlacing')}
        </p>
      </div>
    );
  }

  // Shot feedback: the result of MY latest shot, shown while the match
  // runs. Makes the hit-shoots-again rule legible — "Hit — shoot again!"
  // explains why the turn didn't flip.
  const lastShot = model.myShots.length > 0 ? model.myShots[model.myShots.length - 1] : null;
  const feedback =
    !matchEnded && lastShot
      ? t(`games.seabattle.feedback.${lastShot.result}`)
      : null;

  return (
    <div className="sb sb--battle stack">
      {feedback ? (
        <p
          className={`sb__status ${lastShot?.result === 'miss' ? 'sb__status--muted' : 'sb__status--hit'}`}
          role="status"
        >
          {feedback}
        </p>
      ) : null}
      <div className="sb__tabs" role="tablist" aria-hidden>
        <button
          type="button"
          className={`radio-pill ${activeBoard === 'target' ? 'radio-pill--active' : ''}`}
          onClick={() => scrollToBoard('target')}
        >
          {t('games.seabattle.board.target')}
        </button>
        <button
          type="button"
          className={`radio-pill ${activeBoard === 'fleet' ? 'radio-pill--active' : ''}`}
          onClick={() => scrollToBoard('fleet')}
        >
          {t('games.seabattle.board.yours')}
        </button>
      </div>
      {/* DOM order keeps enemy waters first (first slide of the narrow
          carousel and first in tab order — it's the action board); the
          wide-container row-reverse puts the own fleet on the left, enemy
          waters on the right, matching the paper-game convention. */}
      <div className="sb__boards" ref={boardsRef} onScroll={onBoardsScroll}>
        <TargetGrid
          myShots={model.myShots}
          revealedShips={model.revealedOpponentShips}
          canFire={canPlay && !matchEnded}
          onFire={(x, y) => onSubmitMove({ x, y })}
          revealAll={matchEnded}
          label={t('games.seabattle.board.target')}
          t={{ t, tf }}
        />
        <FleetGrid
          fleet={model.myFleet ?? []}
          shotsAtMe={model.shotsAtMe}
          label={t('games.seabattle.board.yours')}
          t={{ t, tf }}
        />
      </div>
    </div>
  );
};

type Translator = Pick<ReturnType<typeof useTranslator>, 't' | 'tf'>;

function cellAria(
  { tf }: Translator,
  key: 'fire' | 'miss' | 'hit' | 'sunk' | 'ship' | 'water',
  x: number,
  y: number,
): string {
  return tf(`games.seabattle.cell.${key}`, { row: y + 1, col: x + 1 });
}

/**
 * The targeting grid (opponent's waters). Shot results are shape-coded —
 * dot for a miss, ✕ for a hit, filled ship cell for sunk — so the two
 * sides of the exchange stay legible without relying on hue (same
 * accessibility bar as Connect 4's disc/ring rule). Cells deduction
 * proves empty (no-touch neighbors of sunk ships) render dimmed but stay
 * clickable — a legal wasted miss.
 */
function TargetGrid({
  myShots,
  revealedShips,
  canFire,
  onFire,
  revealAll,
  label,
  t,
}: {
  myShots: readonly ShotView[];
  revealedShips: readonly Ship[];
  canFire: boolean;
  onFire: (x: number, y: number) => void;
  revealAll: boolean;
  label: string;
  t: Translator;
}) {
  const shotByCell = new Map<number, ShotView>();
  for (const shot of myShots) shotByCell.set(cellKey(shot.x, shot.y), shot);
  const shipCellSet = new Set<number>();
  for (const ship of revealedShips) {
    for (const cell of shipCells(ship)) shipCellSet.add(cellKey(cell.x, cell.y));
  }
  const dimmed = deducedEmptyCells(revealedShips.filter((s) => !revealAll || shipCellsAllShot(s, shotByCell)));

  return (
    <section className="sb__board">
      <h3 className="sb__board-label">{label}</h3>
      <div className="sb__grid" role="grid" aria-label={label}>
        {Array.from({ length: GRID_SIZE * GRID_SIZE }, (_, i) => {
          const x = i % GRID_SIZE;
          const y = Math.floor(i / GRID_SIZE);
          const shot = shotByCell.get(i);
          const isShip = shipCellSet.has(i);
          const fireable = canFire && !shot;
          const classes = ['sb__cell'];
          if (isShip) classes.push('sb__cell--ship');
          if (shot?.result === 'sunk' || (isShip && shot)) classes.push('sb__cell--sunk');
          if (!shot && dimmed.has(i)) classes.push('sb__cell--deduced');
          if (fireable) classes.push('sb__cell--fireable');

          const aria = shot
            ? cellAria(t, shot.result, x, y)
            : isShip
              ? cellAria(t, 'sunk', x, y)
              : cellAria(t, fireable ? 'fire' : 'water', x, y);

          return (
            <button
              key={i}
              type="button"
              role="gridcell"
              className={classes.join(' ')}
              disabled={!fireable}
              aria-label={aria}
              onClick={fireable ? () => onFire(x, y) : undefined}
            >
              {shot?.result === 'miss' ? <span className="sb__miss" aria-hidden /> : null}
              {shot?.result === 'hit' || shot?.result === 'sunk' ? (
                <span className="sb__hit" aria-hidden>
                  ✕
                </span>
              ) : null}
            </button>
          );
        })}
      </div>
    </section>
  );
}

function shipCellsAllShot(ship: Ship, shotByCell: ReadonlyMap<number, ShotView>): boolean {
  return shipCells(ship).every((c) => shotByCell.has(cellKey(c.x, c.y)));
}

/**
 * The player's own grid: ships filled, the opponent's incoming shots
 * overlaid (dot = their miss, ✕ = their hit). The opponent's latest shot
 * carries the cross-game last-move highlight (platform.md §3).
 */
function FleetGrid({
  fleet,
  shotsAtMe,
  label,
  t,
}: {
  fleet: readonly Ship[];
  shotsAtMe: readonly ShotView[];
  label: string;
  t: Translator;
}) {
  const shipCellSet = new Set<number>();
  for (const ship of fleet) {
    for (const cell of shipCells(ship)) shipCellSet.add(cellKey(cell.x, cell.y));
  }
  const shotByCell = new Map<number, ShotView>();
  for (const shot of shotsAtMe) shotByCell.set(cellKey(shot.x, shot.y), shot);
  const last = shotsAtMe.length > 0 ? shotsAtMe[shotsAtMe.length - 1] : null;
  const lastKey = last ? cellKey(last.x, last.y) : -1;

  return (
    <section className="sb__board">
      <h3 className="sb__board-label">{label}</h3>
      <div className="sb__grid sb__grid--mine" role="grid" aria-label={label}>
        {Array.from({ length: GRID_SIZE * GRID_SIZE }, (_, i) => {
          const x = i % GRID_SIZE;
          const y = Math.floor(i / GRID_SIZE);
          const isShip = shipCellSet.has(i);
          const shot = shotByCell.get(i);
          const classes = ['sb__cell'];
          if (isShip) classes.push('sb__cell--ship');
          if (isShip && shot) classes.push('sb__cell--sunk');
          if (i === lastKey) classes.push('sb__cell--last');

          const aria = shot
            ? cellAria(t, isShip ? 'hit' : 'miss', x, y)
            : cellAria(t, isShip ? 'ship' : 'water', x, y);

          return (
            <div key={i} role="gridcell" className={classes.join(' ')} aria-label={aria}>
              {shot && !isShip ? <span className="sb__miss" aria-hidden /> : null}
              {shot && isShip ? (
                <span className="sb__hit" aria-hidden>
                  ✕
                </span>
              ) : null}
            </div>
          );
        })}
      </div>
    </section>
  );
}
