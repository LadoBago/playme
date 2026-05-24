// Browser-side Sentry init. Runs once on the client per page load.
// Sentry's Next.js SDK looks for this file at the project root.
//
// CLAUDE.md §5.8: send_default_pii: false (IPs are PII in the EU).

import * as Sentry from '@sentry/nextjs';

if (process.env.NEXT_PUBLIC_SENTRY_DSN) {
  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    // 0.1 = sample 10% of transactions (browser navigations, fetches, etc.).
    // Tightened from 1.0 to stay under the free Sentry tier's 10K perf-unit
    // monthly cap once real traffic arrives. Drop to 0 ("errors-only") if
    // it still pushes the cap; bump back up only while investigating a
    // specific perf issue. Replays stay off — heavier on quota and only
    // useful when debugging UX bugs.
    tracesSampleRate: 0.1,
    replaysOnErrorSampleRate: 0,
    replaysSessionSampleRate: 0,
    sendDefaultPii: false,
  });
}

export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
