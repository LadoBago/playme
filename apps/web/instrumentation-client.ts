// Browser-side Sentry init. Runs once on the client per page load.
// Sentry's Next.js SDK looks for this file at the project root.
//
// CLAUDE.md §5.8: send_default_pii: false (IPs are PII in the EU).

import * as Sentry from '@sentry/nextjs';

if (process.env.NEXT_PUBLIC_SENTRY_DSN) {
  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    tracesSampleRate: 0,
    replaysOnErrorSampleRate: 0,
    replaysSessionSampleRate: 0,
    sendDefaultPii: false,
  });
}

export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
