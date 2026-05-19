import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  DEFAULT_THEME,
  STORAGE_KEY,
  applyTheme,
  readStoredTheme,
  writeStoredTheme,
} from './theme-storage';

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

function stubDocument(html: { dataset: Record<string, string> }): void {
  vi.stubGlobal('document', { documentElement: html });
}

describe('readStoredTheme', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('returns the default when window is undefined (SSR)', () => {
    expect(readStoredTheme()).toBe(DEFAULT_THEME);
  });

  it('returns the default when nothing is stored', () => {
    stubWindow(makeStorage());
    expect(readStoredTheme()).toBe(DEFAULT_THEME);
  });

  it('returns the stored value when valid', () => {
    stubWindow(makeStorage({ [STORAGE_KEY]: 'dark' }));
    expect(readStoredTheme()).toBe('dark');
  });

  it('returns the default for unknown stored values', () => {
    stubWindow(makeStorage({ [STORAGE_KEY]: 'neon' }));
    expect(readStoredTheme()).toBe(DEFAULT_THEME);
  });

  it('returns the default when localStorage throws (Safari ITP, private mode)', () => {
    const storage = makeStorage();
    storage.getItem = vi.fn(() => {
      throw new Error('SecurityError');
    });
    stubWindow(storage);
    expect(readStoredTheme()).toBe(DEFAULT_THEME);
  });
});

describe('writeStoredTheme', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('persists the value to localStorage', () => {
    const storage = makeStorage();
    stubWindow(storage);
    writeStoredTheme('dark');
    expect(storage.setItem).toHaveBeenCalledWith(STORAGE_KEY, 'dark');
  });

  it('does not throw when localStorage rejects writes (quota / ITP)', () => {
    const storage = makeStorage();
    storage.setItem = vi.fn(() => {
      throw new Error('QuotaExceededError');
    });
    stubWindow(storage);
    expect(() => writeStoredTheme('dark')).not.toThrow();
  });

  it('is a no-op when window is undefined (SSR)', () => {
    expect(() => writeStoredTheme('dark')).not.toThrow();
  });
});

describe('applyTheme', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('sets data-theme on the document element', () => {
    const html = { dataset: {} as Record<string, string> };
    stubDocument(html);
    applyTheme('dark');
    expect(html.dataset.theme).toBe('dark');
  });

  it('is a no-op when document is undefined (SSR)', () => {
    expect(() => applyTheme('dark')).not.toThrow();
  });
});
