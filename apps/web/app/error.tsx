'use client';

import { useTranslator } from '@/lib/use-locale';

// Root-level error boundary — lives outside [locale] because error.tsx
// renders for any unhandled exception in the tree, including
// pre-segment problems. useTranslator falls back to <html lang>
// (set by the root layout from the middleware-supplied x-locale
// header) when params.locale isn't present, so the message still
// renders in the user's chosen language.

export default function ErrorBoundary({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const { t } = useTranslator();
  return (
    <main className="container stack" style={{ textAlign: 'center', gap: '1rem' }}>
      <h1 style={{ fontSize: '1.6rem' }}>{t('errors.boundary.title')}</h1>
      <p style={{ color: 'var(--fg-muted)' }}>{t('errors.unknown')}</p>
      <pre
        style={{
          color: 'var(--fg-muted)',
          fontSize: '0.85rem',
          textAlign: 'left',
          maxWidth: '40ch',
          margin: '0 auto',
          whiteSpace: 'pre-wrap',
        }}
      >
        {error.message}
      </pre>
      <button type="button" className="button-primary" onClick={reset} style={{ alignSelf: 'center' }}>
        {t('errors.boundary.retry')}
      </button>
    </main>
  );
}
