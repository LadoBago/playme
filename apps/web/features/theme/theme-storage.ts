// Manual theme override (Sprint 6 toggle). `light` / `dark` set
// `<html data-theme>` explicitly; `system` defers to the OS via the
// `prefers-color-scheme` media query in app/globals.css.

export const STORAGE_KEY = 'playme:theme';

export type Theme = 'light' | 'dark' | 'system';

export const DEFAULT_THEME: Theme = 'system';

const VALID: ReadonlySet<Theme> = new Set<Theme>(['light', 'dark', 'system']);

function isTheme(value: unknown): value is Theme {
  return typeof value === 'string' && VALID.has(value as Theme);
}

export function readStoredTheme(): Theme {
  if (typeof window === 'undefined') return DEFAULT_THEME;
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    return isTheme(raw) ? raw : DEFAULT_THEME;
  } catch {
    // localStorage throws under Safari ITP partitioning and in private
    // browsing — fall back to the default rather than crashing.
    return DEFAULT_THEME;
  }
}

export function writeStoredTheme(theme: Theme): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(STORAGE_KEY, theme);
  } catch {
    // Same ITP / private-mode failure mode as readStoredTheme — the
    // DOM still updates, the choice just doesn't persist.
  }
}

export function applyTheme(theme: Theme): void {
  if (typeof document === 'undefined') return;
  document.documentElement.dataset.theme = theme;
}
