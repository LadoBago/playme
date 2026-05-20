'use client';

import { useSyncExternalStore } from 'react';

// Module-level singleton for the PWA install-prompt event. The browser
// fires `beforeinstallprompt` whenever it decides the user is engaged
// enough — possibly on a sub-page the user navigated to before the
// landing page sees them. We init the listener once from the root
// layout (so we never miss a firing) and render the actual button on
// landing only (so the match page stays uncluttered).

const DISMISS_KEY = 'playme:installDismissed';

// Routes where `<InstallPrompt />` actually renders. Kept in sync with
// where the component is mounted in app/[locale]/page.tsx. The locale
// prefix on `/en` is the only non-root form — the default `ka` locale
// stays at `/` per middleware.ts rewrite.
function isLandingPath(pathname: string): boolean {
  return pathname === '/' || pathname === '/en' || pathname === '/en/';
}

// The Web App Manifest spec hasn't formalised this event yet; declared
// locally so we don't import a `@types/wicg-...` package.
interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

type Snapshot = {
  promptable: boolean;
};

let cachedEvent: BeforeInstallPromptEvent | null = null;
let installed = false;
let dismissed = false;
let initialised = false;
const listeners = new Set<() => void>();
let snapshot: Snapshot = { promptable: false };

function recompute(): void {
  snapshot = { promptable: !installed && !dismissed && cachedEvent !== null };
  for (const listener of listeners) listener();
}

export function initInstallStore(): void {
  if (initialised) return;
  if (typeof window === 'undefined') return;
  initialised = true;

  // Already running as an installed PWA — no prompt needed.
  if (window.matchMedia('(display-mode: standalone)').matches) {
    installed = true;
  }

  try {
    if (window.localStorage.getItem(DISMISS_KEY) === '1') dismissed = true;
  } catch {
    // localStorage throws under Safari ITP partitioning and in private
    // browsing — fall through to default (not dismissed).
  }

  window.addEventListener('beforeinstallprompt', (e: Event) => {
    // Only suppress the browser's mini-infobar on routes where we
    // actually render our own UI (`<InstallPrompt />`, landing only).
    // On a deep-linked sub-page — /play/<game>, /r/<code> — calling
    // preventDefault here would hide the native install icon AND
    // leave the visitor with no affordance (since our banner doesn't
    // render outside `/`), which is exactly the UX bug Chrome warns
    // about: "Banner not shown: beforeinstallpromptevent.preventDefault()
    // called. The page must call ... .prompt() to show the banner."
    if (isLandingPath(window.location.pathname)) {
      e.preventDefault();
    }
    // Cache regardless. If the visitor later navigates to landing,
    // <InstallPrompt /> mounts and can still drive `prompt()` against
    // the saved event — beforeinstallprompt only fires once per
    // session in most browsers.
    cachedEvent = e as BeforeInstallPromptEvent;
    recompute();
  });

  window.addEventListener('appinstalled', () => {
    cachedEvent = null;
    installed = true;
    recompute();
  });

  recompute();
}

export async function install(): Promise<void> {
  if (!cachedEvent) return;
  // Clear before awaiting so a double-click can't re-trigger; the
  // browser also invalidates the event once prompt() runs.
  const evt = cachedEvent;
  cachedEvent = null;
  recompute();
  try {
    await evt.prompt();
    // If accepted, `appinstalled` will fire and set installed=true.
    // If dismissed, cachedEvent stays null until the browser refires
    // beforeinstallprompt — which it eventually does on a fresh visit.
  } catch {
    // Some browsers throw if prompt() is called too quickly back-to-
    // back. Swallow — the next fire of beforeinstallprompt will reset
    // cachedEvent and the UI will offer another chance.
  }
}

export function dismiss(): void {
  dismissed = true;
  try {
    window.localStorage.setItem(DISMISS_KEY, '1');
  } catch {
    // ITP — runtime-only dismissal; the prompt will return on the next
    // session, which is acceptable for this surface.
  }
  recompute();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function getSnapshot(): Snapshot {
  return snapshot;
}

// Stable reference required by useSyncExternalStore — returning a
// fresh object each call trips React's "infinite loop" guard during
// SSR / hydration.
const SERVER_SNAPSHOT: Snapshot = { promptable: false };

function getServerSnapshot(): Snapshot {
  return SERVER_SNAPSHOT;
}

export function useInstallStatus(): Snapshot {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
