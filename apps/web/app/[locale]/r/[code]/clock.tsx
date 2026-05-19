'use client';

import { useEffect, useRef, useState } from 'react';
import type { ClockSnapshotDto, Role } from '@playme/shared';
import { extrapolateClock, formatClock } from '@/lib/clock';
import { useTranslator } from '@/lib/use-locale';

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
export function Clock({ snapshot, callerRole, isFinal }: ClockProps) {
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

  return (
    <div className="match-clock" role="group" aria-label={t('match.clock.label')}>
      <ClockFace label={t('match.you')} ms={youMs} active={youActive} />
      <ClockFace label={t('match.opponent')} ms={opponentMs} active={opponentActive} />
    </div>
  );
}

const LOW_TIME_MS = 10_000;

function ClockFace({
  label,
  ms,
  active,
}: {
  label: string;
  ms: number;
  active: boolean;
}) {
  const classNames = ['match-clock__side'];
  if (active) classNames.push('match-clock__side--active');
  if (active && ms <= LOW_TIME_MS) classNames.push('match-clock__side--low');

  return (
    <div className={classNames.join(' ')} aria-live={active ? 'polite' : 'off'}>
      <span className="match-clock__role">{label}</span>
      <span className="match-clock__time">{formatClock(ms)}</span>
    </div>
  );
}
