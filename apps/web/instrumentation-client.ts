// Browser-side Sentry init. Runs once on the client per page load.
// Sentry's Next.js SDK looks for this file at the project root.
//
// CLAUDE.md §5.8: send_default_pii: false (IPs are PII in the EU).

import * as Sentry from '@sentry/nextjs';

if (process.env.NEXT_PUBLIC_SENTRY_DSN) {
  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    // 1.0 = sample every transaction (browser navigations, fetches, etc.).
    // Fine for v1 traffic. The free Sentry tier caps transactions per
    // month; if usage approaches the cap, drop this to a fractional rate
    // (e.g. 0.1) or back to 0 ("errors-only"). Replays stay off — those
    // are heavier on quota and useful only when debugging UX bugs.
    tracesSampleRate: 1.0,
    replaysOnErrorSampleRate: 0,
    replaysSessionSampleRate: 0,
    sendDefaultPii: false,
  });
}

export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
