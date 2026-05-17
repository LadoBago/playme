'use client';

import { t } from '@playme/shared';

export default function ErrorBoundary({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
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
