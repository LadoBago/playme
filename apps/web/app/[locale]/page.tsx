import Link from 'next/link';
import { GAME_CATALOG, createTranslator, localizedHref } from '@playme/shared';
import { Wordmark } from '@/features/branding/wordmark';
import { InstallPrompt } from '@/features/pwa/install-prompt';
import { resolveLocale } from '@/lib/locale';

// SSR + indexable (CLAUDE.md §2.5 SEO). /en/ and / (ka) both route
// through this segment; the active locale comes from params.locale,
// set by middleware.ts for the default-locale rewrite.

interface PageProps {
  params: Promise<{ locale: string }>;
}

export default async function HomePage({ params }: PageProps) {
  const locale = await resolveLocale(params);
  const { t } = createTranslator(locale);

  return (
    <main className="container stack" style={{ gap: '2.5rem' }}>
      <InstallPrompt />

      <section
        className="stack"
        style={{ gap: '0.5rem', textAlign: 'center', alignItems: 'center' }}
      >
        <h1 style={{ margin: 0 }}>
          <span className="visually-hidden">playme.ge</span>
          <Wordmark size="3.25rem" />
        </h1>
        <p
          style={{
            color: 'var(--accent)',
            fontWeight: 500,
            margin: 0,
            fontSize: '1.1rem',
          }}
        >
          {t('site.brandTagline')}
        </p>
        <p style={{ color: 'var(--fg-muted)', margin: 0 }}>{t('site.tagline')}</p>
      </section>

      <section className="stack">
        <h2 style={{ fontSize: '1.4rem' }}>{t('site.catalog.title')}</h2>
        <div className="catalog-grid">
          {GAME_CATALOG.map((game) => (
            <Link
              key={game.id}
              href={localizedHref(`/play/${game.slug}`, locale)}
              className="game-card"
            >
              <BoardPreview rows={game.rows} cols={game.cols} />
              <span className="game-card__title">{t(game.nameKey)}</span>
              <span className="game-card__desc">{t(game.shortDescriptionKey)}</span>
            </Link>
          ))}
        </div>
      </section>

      <section className="stack">
        <h2 style={{ fontSize: '1.4rem' }}>{t('site.howItWorks.title')}</h2>
        <div className="howitworks">
          <Step n={1} title={t('site.howItWorks.step1.title')} body={t('site.howItWorks.step1.body')} />
          <Step n={2} title={t('site.howItWorks.step2.title')} body={t('site.howItWorks.step2.body')} />
          <Step n={3} title={t('site.howItWorks.step3.title')} body={t('site.howItWorks.step3.body')} />
        </div>
      </section>
    </main>
  );
}

function Step({ n, title, body }: { n: number; title: string; body: string }) {
  return (
    <div className="howitworks__step">
      <span className="howitworks__num">{n}</span>
      <strong>{title}</strong>
      <span style={{ color: 'var(--fg-muted)' }}>{body}</span>
    </div>
  );
}

function BoardPreview({ rows, cols }: { rows: number; cols: number }) {
  // Decorative-only: a simple grid silhouette so each card has a visual
  // anchor even before we ship per-game illustrations in Sprint 6.
  const cells = Array.from({ length: rows * cols }, (_, i) => i);
  return (
    <div
      aria-hidden
      style={{
        display: 'grid',
        gridTemplateColumns: `repeat(${cols}, 1fr)`,
        gap: 3,
        width: 80,
        height: 80,
        margin: '0 auto 0.5rem',
      }}
    >
      {cells.map((i) => (
        <div
          key={i}
          style={{
            background: 'var(--highlight)',
            border: '1px solid var(--border)',
            borderRadius: 3,
          }}
        />
      ))}
    </div>
  );
}
