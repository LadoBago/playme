import Link from 'next/link';
import { localizedHref } from '@playme/shared';
import { getServerTranslator } from '@/lib/locale';

export default async function NotFound() {
  const { t, locale } = await getServerTranslator();
  return (
    <main className="container stack" style={{ textAlign: 'center', gap: '1rem' }}>
      <h1 style={{ fontSize: '2rem' }}>404</h1>
      <p style={{ color: 'var(--fg-muted)' }}>{t('errors.room.notFound')}</p>
      <Link
        href={localizedHref('/', locale)}
        className="button-primary"
        style={{ alignSelf: 'center', textDecoration: 'none' }}
      >
        {t('notFound.home')}
      </Link>
    </main>
  );
}
