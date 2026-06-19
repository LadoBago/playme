'use client';

import { useEffect, useRef, useState } from 'react';
import { LocaleToggle } from '@/features/locale/locale-toggle';
import { ThemeToggle } from '@/features/theme/theme-toggle';
import { useTranslator } from '@/lib/use-locale';

/**
 * Fixed top-right utility cluster: the locale switcher + the theme toggle.
 *
 * On wide viewports both toggles sit inline (the settings trigger is hidden
 * by CSS). On narrow viewports they'd crowd the corner — and overlap the
 * board on a match page — so they fold behind a single settings button and
 * drop down as a small panel when opened. The fold is CSS-driven off the
 * `.toolbar__panel--open` class; the open state only matters on narrow,
 * where the media query reveals the trigger and hides the closed panel.
 */
export function Toolbar() {
  const { t } = useTranslator();
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // Close on outside-click or Escape while open (same pattern as the emote
  // picker). Bound only while open so there's no idle listener cost.
  useEffect(() => {
    if (!open) return undefined;
    const onPointerDown = (e: MouseEvent) => {
      if (!containerRef.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  return (
    <div className="toolbar" ref={containerRef}>
      <button
        type="button"
        className="toolbar__trigger"
        aria-label={t('toolbar.settings')}
        aria-haspopup="true"
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
      >
        <SettingsIcon />
      </button>
      <div className={open ? 'toolbar__panel toolbar__panel--open' : 'toolbar__panel'}>
        <LocaleToggle />
        <ThemeToggle />
      </div>
    </div>
  );
}

/** Feather-style gear, matching the theme toggle's inline-SVG icons. */
function SettingsIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      width="18"
      height="18"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  );
}
