import type { ClockSnapshotDto } from '@playme/shared';

/**
 * Compute the displayed-remaining time for both players given a server
 * clock snapshot and the current local moment. Mirrors the lazy clock
 * model in state.md §2.2:
 *
 * - For the active player: subtract elapsed since `serverNowAt` from
 *   their stored remaining time. We use `serverNowAt` (not `lastTickAt`)
 *   as the reference: the server stamps `serverNowAt` at serialize time,
 *   so `serverMs - elapsedSinceServerNow` always gives the right answer
 *   without the client having to estimate clock skew.
 * - For the inactive player: stored remaining is unchanged.
 *
 * Both values are floored at zero — the UI shows `0:00` when the active
 * clock crosses the deadline, and the server's timeout sweeper takes
 * over.
 *
 * `localNowMs` is taken as a parameter (rather than reading `Date.now()`
 * directly) so the function is deterministic and unit-testable.
 */
export function extrapolateClock(
  snapshot: ClockSnapshotDto,
  receivedAtMs: number,
  localNowMs: number,
): { hostMs: number; challengerMs: number } {
  const elapsedSinceReceived = Math.max(0, localNowMs - receivedAtMs);
  const activeStored =
    snapshot.activePlayer === 'host' ? snapshot.hostMs : snapshot.challengerMs;
  const activeDisplayed = Math.max(0, activeStored - elapsedSinceReceived);

  return snapshot.activePlayer === 'host'
    ? { hostMs: activeDisplayed, challengerMs: snapshot.challengerMs }
    : { hostMs: snapshot.hostMs, challengerMs: activeDisplayed };
}

/**
 * Format a millisecond duration as a colon-separated mm:ss string. Used
 * by the clock display; broken out for direct unit testing.
 */
export function formatClock(ms: number): string {
  const totalSeconds = Math.max(0, Math.floor(ms / 1000));
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}
