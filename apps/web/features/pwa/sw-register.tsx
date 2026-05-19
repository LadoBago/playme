'use client';

import { useEffect } from 'react';

// Registers the service worker (Sprint 6 PWA) on mount. Only fires in
// production — in dev, Next.js's HMR routes around the SW and the
// cache-first rules can hide live source edits. Re-enable in dev only
// for a deliberate test pass.
export function ServiceWorkerRegister() {
  useEffect(() => {
    if (process.env.NODE_ENV !== 'production') return;
    if (typeof navigator === 'undefined') return;
    if (!('serviceWorker' in navigator)) return;

    navigator.serviceWorker.register('/sw.js').catch((err) => {
      // SW registration is best-effort — the site works fine without
      // it (no offline support, no install prompt). Warn so prod
      // Sentry breadcrumbs pick it up, but don't surface anything to
      // the user.
      console.warn('[PlayMe] service worker registration failed:', err);
    });
  }, []);

  return null;
}
