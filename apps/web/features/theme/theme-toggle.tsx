'use client';

import { t, type I18nKey } from '@playme/shared';
import type { Theme } from './theme-storage';
import { useTheme } from './use-theme';

function nextTheme(current: Theme): Theme {
  switch (current) {
    case 'light':
      return 'dark';
    case 'dark':
      return 'system';
    case 'system':
      return 'light';
  }
}

function ariaKeyForNext(current: Theme): I18nKey {
  switch (current) {
    case 'light':
      return 'theme.toggle.next.dark';
    case 'dark':
      return 'theme.toggle.next.system';
    case 'system':
      return 'theme.toggle.next.light';
  }
}

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();

  // Pre-mount: render nothing. The toggle is position: fixed so its
  // absence doesn't shift any layout, and the FOUC script has already
  // applied the right theme to the page.
  if (theme === null) return null;

  return (
    <button
      type="button"
      className="theme-toggle"
      onClick={() => setTheme(nextTheme(theme))}
      aria-label={t(ariaKeyForNext(theme))}
    >
      {theme === 'light' ? <SunIcon /> : theme === 'dark' ? <MoonIcon /> : <MonitorIcon />}
    </button>
  );
}

// Inline SVGs (Feather-style). currentColor lets the icon track the
// button's text color so light/dark legibility comes free from the
// theme tokens.

function SunIcon() {
  return (
    <svg
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
    </svg>
  );
}

function MoonIcon() {
  return (
    <svg
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
    </svg>
  );
}

function MonitorIcon() {
  return (
    <svg
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <rect x="2" y="3" width="20" height="14" rx="2" />
      <path d="M8 21h8M12 17v4" />
    </svg>
  );
}
