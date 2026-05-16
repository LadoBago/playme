// Next.js instrumentation hook (App Router). Runs once per server-side
// runtime (Node + Edge); the browser-side init lives in
// `instrumentation-client.ts`.
//
// CLAUDE.md §5.8: send_default_pii is OFF.

export async function register() {
  if (!process.env.NEXT_PUBLIC_SENTRY_DSN) return;

  if (process.env.NEXT_RUNTIME === 'nodejs') {
    const Sentry = await import('@sentry/nextjs');
    Sentry.init({
      dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
      tracesSampleRate: 1.0,
      sendDefaultPii: false,
    });
  }

  if (process.env.NEXT_RUNTIME === 'edge') {
    const Sentry = await import('@sentry/nextjs');
    Sentry.init({
      dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
      tracesSampleRate: 1.0,
      sendDefaultPii: false,
    });
  }
}

export { captureRequestError as onRequestError } from '@sentry/nextjs';
