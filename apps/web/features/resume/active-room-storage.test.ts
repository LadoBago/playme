import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ACTIVE_ROOM_KEY,
  clearActiveRoom,
  isResumableStatus,
  readActiveRoom,
  writeActiveRoom,
} from './active-room-storage';

type StorageStub = {
  getItem: (key: string) => string | null;
  setItem: (key: string, value: string) => void;
  removeItem: (key: string) => void;
};

function makeStorage(initial: Record<string, string> = {}): StorageStub & {
  store: Map<string, string>;
} {
  const store = new Map<string, string>(Object.entries(initial));
  return {
    store,
    getItem: vi.fn((k: string) => store.get(k) ?? null),
    setItem: vi.fn((k: string, v: string) => {
      store.set(k, v);
    }),
    removeItem: vi.fn((k: string) => {
      store.delete(k);
    }),
  };
}

function stubWindow(localStorage: StorageStub): void {
  vi.stubGlobal('window', { localStorage });
}

const sample = { code: 'abc123', gameId: 'reversi', role: 'host' as const, savedAt: 1_700_000_000_000 };

describe('readActiveRoom', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('returns null when window is undefined (SSR)', () => {
    expect(readActiveRoom()).toBeNull();
  });

  it('returns null when nothing is stored', () => {
    stubWindow(makeStorage());
    expect(readActiveRoom()).toBeNull();
  });

  it('round-trips a written room', () => {
    stubWindow(makeStorage());
    writeActiveRoom(sample);
    expect(readActiveRoom()).toEqual(sample);
  });

  it('returns null for malformed JSON', () => {
    stubWindow(makeStorage({ [ACTIVE_ROOM_KEY]: '{not json' }));
    expect(readActiveRoom()).toBeNull();
  });

  it('returns null when a field fails validation', () => {
    stubWindow(
      makeStorage({
        [ACTIVE_ROOM_KEY]: JSON.stringify({ ...sample, role: 'spectator' }),
      }),
    );
    expect(readActiveRoom()).toBeNull();
  });

  it('returns null when storage access throws', () => {
    stubWindow({
      getItem: () => {
        throw new Error('SecurityError');
      },
      setItem: vi.fn(),
      removeItem: vi.fn(),
    });
    expect(readActiveRoom()).toBeNull();
  });
});

describe('clearActiveRoom', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('is a no-op when window is undefined (SSR)', () => {
    expect(() => clearActiveRoom()).not.toThrow();
  });

  it('removes the stored room unconditionally without a code', () => {
    const storage = makeStorage({ [ACTIVE_ROOM_KEY]: JSON.stringify(sample) });
    stubWindow(storage);
    clearActiveRoom();
    expect(storage.store.has(ACTIVE_ROOM_KEY)).toBe(false);
  });

  it('removes the stored room when the code matches', () => {
    const storage = makeStorage({ [ACTIVE_ROOM_KEY]: JSON.stringify(sample) });
    stubWindow(storage);
    clearActiveRoom(sample.code);
    expect(storage.store.has(ACTIVE_ROOM_KEY)).toBe(false);
  });

  it('keeps the stored room when the code is a different room', () => {
    const storage = makeStorage({ [ACTIVE_ROOM_KEY]: JSON.stringify(sample) });
    stubWindow(storage);
    clearActiveRoom('other-room');
    expect(readActiveRoom()).toEqual(sample);
  });

  it('swallows storage errors', () => {
    stubWindow({
      getItem: vi.fn(() => null),
      setItem: vi.fn(),
      removeItem: () => {
        throw new Error('SecurityError');
      },
    });
    expect(() => clearActiveRoom()).not.toThrow();
  });
});

describe('isResumableStatus', () => {
  it('is true only for the pre-match and in-match states', () => {
    expect(isResumableStatus('waitingForOpponent')).toBe(true);
    expect(isResumableStatus('settingUp')).toBe(true);
    expect(isResumableStatus('inProgress')).toBe(true);
    expect(isResumableStatus('ended')).toBe(false);
    expect(isResumableStatus('awaitingRematch')).toBe(false);
    expect(isResumableStatus('closed')).toBe(false);
    expect(isResumableStatus('expired')).toBe(false);
  });
});
