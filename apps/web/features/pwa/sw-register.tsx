'use client';

import { useEffect } from 'react';

// Registers the service worker (Sprint 6 PWA) on mount. Only fires in
// production — in dev, Next.js's HMR routes around the SW and the
// cache-first rules can hide live source edits.
//
// In dev it goes one step further and actively UNREGISTERS any worker
// left over from a previous production session (`next start` on the same
// origin — e.g. a Lighthouse pass) and purges its caches. Merely skipping
// registration isn't enough: a lingering prod SW keeps serving
// `/_next/static/*` cache-first, and Turbopack's dev chunk names are
// path-stable, so the browser gets stale code while SSR is fresh —
// presenting as hydration mismatches that survive normal reloads
// (Safari's reload doesn't bypass the SW at all). Bitten 2026-06-04
// during the Sprint 10 smoke test.
export function ServiceWorkerRegister() {
  useEffect(() => {
    if (typeof navigator === 'undefined') return;
    if (!('serviceWorker' in navigator)) return;

    if (process.env.NODE_ENV !== 'production') {
      void (async () => {
        try {
          const registrations = await navigator.serviceWorker.getRegistrations();
          await Promise.all(registrations.map((r) => r.unregister()));
          if ('caches' in globalThis) {
            const keys = await caches.keys();
            await Promise.all(keys.map((k) => caches.delete(k)));
          }
          if (registrations.length > 0) {
            // One reload is still needed for the page that was already
            // loaded through the old worker; log instead of forcing it.
            console.info(
              '[PlayMe] dev: unregistered a leftover service worker — reload once for fresh assets.',
            );
          }
        } catch {
          // Best-effort cleanup; never break dev over it.
        }
      })();
      return;
    }

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
