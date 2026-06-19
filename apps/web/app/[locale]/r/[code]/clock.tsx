'use client';

import { useEffect, useRef, useState } from 'react';
import type { ClockSnapshotDto, Role, ScoreDto } from '@playme/shared';
import { extrapolateClock, formatClock } from '@/lib/clock';
import { useTranslator } from '@/lib/use-locale';
import { EmoteBubble, type IncomingEmote } from '@/features/emote';

interface ClockProps {
  snapshot: ClockSnapshotDto;
  /** The caller's role; null while presence is still being resolved. */
  callerRole: Role | null;
  /**
   * True when <c>match.outcome != null</c>. Server-side, a move that
   * ends the match still flips the active player + advances the clock
   * one last time before stamping the outcome (state.md §2.2 — the
   * post-move clock is the authoritative "as of match end" snapshot).
   * Without this flag the client would keep extrapolating the now-
   * "active" side's clock past the moment of victory.
   */
  isFinal: boolean;
  /**
   * Latest emote the opponent sent — rendered as a transient bubble over
   * the opponent's clock face (the one strip that's always present and in
   * the same spot whenever emotes are allowed). Null when none is showing.
   */
  opponentEmote?: IncomingEmote | null;
  /** Series score, shown between the two clock faces from the viewer's
   *  perspective (left = you, right = opponent). */
  score: ScoreDto;
}

/**
 * Per-player chess clock. Reads the server-authoritative snapshot from the
 * room state and extrapolates the active player's remaining time locally
 * between snapshots — see <see cref="extrapolateClock"/> and state.md §2.2.
 * The component re-renders every 100 ms; that's well below human
 * perception of clock motion and far cheaper than animating at 60 fps.
 *
 * When a new snapshot arrives (move accepted, reconnect, etc.) the parent
 * passes a fresh <see cref="ClockSnapshotDto"/> instance; we capture the
 * local moment of arrival in a ref so extrapolation re-aligns to the
 * new server time without trusting the client's wall clock against
 * <c>lastTickAt</c> directly.
 */
export function Clock({
  snapshot,
  callerRole,
  isFinal,
  opponentEmote = null,
  score,
}: ClockProps) {
  const { t } = useTranslator();
  const receivedAtRef = useRef<number>(Date.now());
  // Reset the local reference whenever a new snapshot reference comes in.
  // We can't use snapshot.serverNowAt directly — it's the server's wall
  // clock, which may not match the client's. The local Date.now() at the
  // moment the snapshot arrived is the right thing to extrapolate against.
  const previousSnapshotRef = useRef(snapshot);
  if (previousSnapshotRef.current !== snapshot) {
    previousSnapshotRef.current = snapshot;
    receivedAtRef.current = Date.now();
  }

  const [now, setNow] = useState<number>(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 100);
    return () => window.clearInterval(id);
  }, []);

  // Final state freezes both faces at the server's last-broadcast snapshot;
  // skipping extrapolation here avoids the "loser's clock keeps ticking
  // past 0" bug.
  const { hostMs, challengerMs } = isFinal
    ? { hostMs: snapshot.hostMs, challengerMs: snapshot.challengerMs }
    : extrapolateClock(snapshot, receivedAtRef.current, now);

  // Render the caller's clock on the left, opponent's on the right —
  // matches MatchHeader's "you / opponent" ordering so the two strips
  // line up. Challenger sees their own face first; host sees theirs.
  const youIsHost = callerRole === 'host';
  const youMs = youIsHost ? hostMs : challengerMs;
  const opponentMs = youIsHost ? challengerMs : hostMs;
  const youActive =
    !isFinal && callerRole != null && snapshot.activePlayer === callerRole;
  const opponentActive =
    !isFinal && callerRole != null && snapshot.activePlayer !== callerRole;

  // Score from the viewer's perspective so the left count sits under the
  // left (you) clock. Host-on-left fallback while role is still null.
  const myWins = callerRole === 'challenger' ? score.challenger : score.host;
  const opponentWins = callerRole === 'challenger' ? score.host : score.challenger;

  return (
    <div className="match-clock" role="group" aria-label={t('match.clock.label')}>
      <ClockFace label={t('match.you')} ms={youMs} active={youActive} />
      <SeriesScore myWins={myWins} opponentWins={opponentWins} draws={score.draws} />
      <ClockFace label={t('match.opponent')} ms={opponentMs} active={opponentActive}>
        <EmoteBubble emote={opponentEmote} />
      </ClockFace>
    </div>
  );
}

/**
 * Centred series scoreboard between the two clock faces. Reads from the
 * viewer's perspective: left number = you, right number = opponent. The
 * draws subtitle appears only when draws > 0. Exported so the match header
 * can render the same scoreboard during the setup phase, when no clock (and
 * thus no clock-row score) is shown.
 */
export function SeriesScore({
  myWins,
  opponentWins,
  draws,
}: {
  myWins: number;
  opponentWins: number;
  draws: number;
}) {
  const { t, tf } = useTranslator();
  // role="group" — aria-label is prohibited on a generic div (WCAG 4.1.2).
  return (
    <div className="match-score" role="group" aria-label={t('match.score.label')}>
      <span className="match-score__counts">
        {myWins}
        <span className="match-score__dash" aria-hidden="true">
          {' – '}
        </span>
        {opponentWins}
      </span>
      {draws > 0 ? (
        <span className="match-score__draws">
          {draws === 1 ? t('match.score.draws.one') : tf('match.score.draws.other', { count: draws })}
        </span>
      ) : null}
    </div>
  );
}

const LOW_TIME_MS = 10_000;

function ClockFace({
  label,
  ms,
  active,
  children,
}: {
  label: string;
  ms: number;
  active: boolean;
  /** Overlay content anchored to this face (e.g. the opponent emote bubble). */
  children?: React.ReactNode;
}) {
  const classNames = ['match-clock__side'];
  if (active) classNames.push('match-clock__side--active');
  if (active && ms <= LOW_TIME_MS) classNames.push('match-clock__side--low');

  return (
    <div className={classNames.join(' ')} aria-live={active ? 'polite' : 'off'}>
      <span className="match-clock__role">{label}</span>
      <span className="match-clock__time">{formatClock(ms)}</span>
      {children}
    </div>
  );
}
