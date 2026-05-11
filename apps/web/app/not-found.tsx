import Link from 'next/link';
import { t } from '@playme/shared';

export default function NotFound() {
  return (
    <main className="container stack" style={{ textAlign: 'center', gap: '1rem' }}>
      <h1 style={{ fontSize: '2rem' }}>404</h1>
      <p style={{ color: 'var(--fg-muted)' }}>{t('errors.room.notFound')}</p>
      <Link href="/" className="button-primary" style={{ alignSelf: 'center', textDecoration: 'none' }}>
        ← Home
      </Link>
    </main>
  );
}
