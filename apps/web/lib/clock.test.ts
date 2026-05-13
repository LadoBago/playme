import { describe, it, expect } from 'vitest';
import { extrapolateClock, formatClock } from './clock';
import type { ClockSnapshotDto } from '@playme/shared';

const snapshotAt = '2026-05-13T12:00:00.000Z';

const baseSnapshot: ClockSnapshotDto = {
  hostMs: 60_000,
  challengerMs: 60_000,
  activePlayer: 'host',
  lastTickAt: snapshotAt,
  serverNowAt: snapshotAt,
};

describe('extrapolateClock', () => {
  const receivedAt = 1_000_000; // arbitrary base for local time

  it('only decrements the active player as local time advances', () => {
    const result = extrapolateClock(baseSnapshot, receivedAt, receivedAt + 5_000);
    expect(result.hostMs).toBe(55_000);
    expect(result.challengerMs).toBe(60_000);
  });

  it('switches which side ticks when activePlayer flips', () => {
    const snap: ClockSnapshotDto = { ...baseSnapshot, activePlayer: 'challenger' };
    const result = extrapolateClock(snap, receivedAt, receivedAt + 5_000);
    expect(result.hostMs).toBe(60_000);
    expect(result.challengerMs).toBe(55_000);
  });

  it('floors at zero rather than going negative', () => {
    const result = extrapolateClock(baseSnapshot, receivedAt, receivedAt + 120_000);
    expect(result.hostMs).toBe(0);
    expect(result.challengerMs).toBe(60_000);
  });

  it('ignores backwards clock jumps (local time before received)', () => {
    // Should not produce a value above the stored remaining.
    const result = extrapolateClock(baseSnapshot, receivedAt, receivedAt - 5_000);
    expect(result.hostMs).toBe(60_000);
    expect(result.challengerMs).toBe(60_000);
  });

  it('treats a freshly received snapshot as the source of truth', () => {
    const result = extrapolateClock(baseSnapshot, receivedAt, receivedAt);
    expect(result.hostMs).toBe(60_000);
    expect(result.challengerMs).toBe(60_000);
  });
});

describe('formatClock', () => {
  it('renders minutes and zero-padded seconds', () => {
    expect(formatClock(60_000)).toBe('1:00');
    expect(formatClock(65_000)).toBe('1:05');
    expect(formatClock(5_000)).toBe('0:05');
    expect(formatClock(0)).toBe('0:00');
  });

  it('floors at zero for negative input', () => {
    expect(formatClock(-1_000)).toBe('0:00');
  });

  it('rounds down sub-second precision', () => {
    expect(formatClock(1_750)).toBe('0:01');
  });
});
