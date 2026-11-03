import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  DISPLAY_NAME_KEY,
  MAX_DISPLAY_NAME_LENGTH,
  gamePrefsKey,
  readStoredDisplayName,
  readStoredGamePrefs,
  writeStoredDisplayName,
  writeStoredGamePrefs,
} from './preferences-storage';

type StorageStub = {
  getItem: (key: string) => string | null;
  setItem: (key: string, value: string) => void;
};

function makeStorage(initial: Record<string, string> = {}): StorageStub {
  const store = new Map<string, string>(Object.entries(initial));
  return {
    getItem: vi.fn((k: string) => store.get(k) ?? null),
    setItem: vi.fn((k: string, v: string) => {
      store.set(k, v);
    }),
  };
}

function stubWindow(localStorage: StorageStub): void {
  vi.stubGlobal('window', { localStorage });
}

describe('readStoredDisplayName', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('returns an empty string when window is undefined (SSR)', () => {
    expect(readStoredDisplayName()).toBe('');
  });

  it('returns an empty string when nothing is stored', () => {
    stubWindow(makeStorage());
    expect(readStoredDisplayName()).toBe('');
  });

  it('returns the stored name trimmed and capped', () => {
    const long = 'x'.repeat(MAX_DISPLAY_NAME_LENGTH + 10);
    stubWindow(makeStorage({ [DISPLAY_NAME_KEY]: `  ${long}  ` }));
    expect(readStoredDisplayName()).toBe('x'.repeat(MAX_DISPLAY_NAME_LENGTH));
  });

  it('returns an empty string when localStorage throws', () => {
    const storage = makeStorage();
    storage.getItem = vi.fn(() => {
      throw new Error('SecurityError');
    });
    stubWindow(storage);
    expect(readStoredDisplayName()).toBe('');
  });
});

describe('writeStoredDisplayName', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('persists the trimmed name', () => {
    const storage = makeStorage();
    stubWindow(storage);
    writeStoredDisplayName('  Lado ');
    expect(storage.setItem).toHaveBeenCalledWith(DISPLAY_NAME_KEY, 'Lado');
  });

  it('ignores a blank name so a stored one is not wiped', () => {
    const storage = makeStorage();
    stubWindow(storage);
    writeStoredDisplayName('   ');
    expect(storage.setItem).not.toHaveBeenCalled();
  });

  it('does not throw when localStorage rejects writes', () => {
    const storage = makeStorage();
    storage.setItem = vi.fn(() => {
      throw new Error('QuotaExceededError');
    });
    stubWindow(storage);
    expect(() => writeStoredDisplayName('Lado')).not.toThrow();
  });

  it('is a no-op when window is undefined (SSR)', () => {
    expect(() => writeStoredDisplayName('Lado')).not.toThrow();
  });
});

describe('readStoredGamePrefs', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('returns {} when window is undefined (SSR)', () => {
    expect(readStoredGamePrefs('tictactoe')).toEqual({});
  });

  it('returns {} when nothing is stored for the game', () => {
    stubWindow(makeStorage());
    expect(readStoredGamePrefs('tictactoe')).toEqual({});
  });

  it('round-trips a full prefs object', () => {
    const storage = makeStorage();
    stubWindow(storage);
    writeStoredGamePrefs('tictactoe', {
      opponent: 'bot',
      botLevel: 'hard',
      sideMode: 'hostPicksSpecific',
      hostSide: 'x',
      boardSize: 6,
    });
    expect(storage.setItem).toHaveBeenCalledWith(gamePrefsKey('tictactoe'), expect.any(String));
    expect(readStoredGamePrefs('tictactoe')).toEqual({
      opponent: 'bot',
      botLevel: 'hard',
      sideMode: 'hostPicksSpecific',
      hostSide: 'x',
      boardSize: 6,
    });
  });

  it('keeps prefs separate per game', () => {
    stubWindow(makeStorage());
    writeStoredGamePrefs('tictactoe', { opponent: 'bot' });
    writeStoredGamePrefs('connect4', { opponent: 'friend' });
    expect(readStoredGamePrefs('tictactoe').opponent).toBe('bot');
    expect(readStoredGamePrefs('connect4').opponent).toBe('friend');
  });

  it('drops only the invalid fields of a stale blob', () => {
    stubWindow(
      makeStorage({
        [gamePrefsKey('tictactoe')]: JSON.stringify({
          opponent: 'alien',
          botLevel: 'medium',
          sideMode: 42,
          hostSide: '',
          boardSize: -3,
        }),
      }),
    );
    expect(readStoredGamePrefs('tictactoe')).toEqual({ botLevel: 'medium' });
  });

  it('returns {} for malformed JSON', () => {
    stubWindow(makeStorage({ [gamePrefsKey('tictactoe')]: '{not json' }));
    expect(readStoredGamePrefs('tictactoe')).toEqual({});
  });

  it('returns {} when the stored value is not an object', () => {
    stubWindow(makeStorage({ [gamePrefsKey('tictactoe')]: '"bot"' }));
    expect(readStoredGamePrefs('tictactoe')).toEqual({});
  });

  it('returns {} when localStorage throws', () => {
    const storage = makeStorage();
    storage.getItem = vi.fn(() => {
      throw new Error('SecurityError');
    });
    stubWindow(storage);
    expect(readStoredGamePrefs('tictactoe')).toEqual({});
  });
});

describe('writeStoredGamePrefs', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('does not throw when localStorage rejects writes', () => {
    const storage = makeStorage();
    storage.setItem = vi.fn(() => {
      throw new Error('QuotaExceededError');
    });
    stubWindow(storage);
    expect(() => writeStoredGamePrefs('tictactoe', { opponent: 'bot' })).not.toThrow();
  });

  it('is a no-op when window is undefined (SSR)', () => {
    expect(() => writeStoredGamePrefs('tictactoe', { opponent: 'bot' })).not.toThrow();
  });
});
