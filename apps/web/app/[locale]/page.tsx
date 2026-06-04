import Link from 'next/link';
import type { Metadata } from 'next';
import { GAME_CATALOG, createTranslator, localizedHref } from '@playme/shared';
import { Wordmark } from '@/features/branding/wordmark';
import { InstallPrompt } from '@/features/pwa/install-prompt';
import { JsonLd } from '@/features/seo/json-ld';
import { resolveLocale } from '@/lib/locale';
import { buildWebSiteSchema } from '@/lib/structured-data';

// SSR + indexable (CLAUDE.md §2.5 SEO). /en/ and / (ka) both route
// through this segment; the active locale comes from params.locale,
// set by middleware.ts for the default-locale rewrite.

interface PageProps {
  params: Promise<{ locale: string }>;
}

// Homepage overrides only its description (meta + OG + Twitter) with the
// landing-specific `site.homeMetaDescription`. `metadataBase`, `robots`,
// and `alternates` are inherited from the root layout, whose canonical /
// hreflang already resolve correctly for `/` and `/en`. openGraph/twitter
// are re-declared in full because metadata objects overwrite (not deep
// merge) across segments — the explicit locale-pinned image mirrors the
// layout to keep a single og:image tag (see app/layout.tsx).
export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const locale = await resolveLocale(params);
  const { t } = createTranslator(locale);
  const description = t('site.homeMetaDescription');
  const canonical = locale === 'ka' ? '/' : '/en';
  const image = {
    url: `/opengraph-image/${locale}`,
    alt: t('site.ogImageAlt'),
  };
  return {
    description,
    openGraph: {
      type: 'website',
      siteName: 'PlayMe',
      title: t('site.title'),
      description,
      url: canonical,
      locale: locale === 'ka' ? 'ka_GE' : 'en_US',
      images: [{ ...image, width: 1200, height: 630 }],
    },
    twitter: {
      card: 'summary_large_image',
      title: t('site.title'),
      description,
      images: [image],
    },
  };
}

export default async function HomePage({ params }: PageProps) {
  const locale = await resolveLocale(params);
  const { t, tf } = createTranslator(locale);
  // Request-time render (the root layout pins `dynamic = 'force-dynamic'`
  // for the whole subtree) keeps the year current without a redeploy.
  const year = new Date().getFullYear();

  return (
    <main className="container stack" style={{ gap: '2.5rem' }}>
      <JsonLd data={buildWebSiteSchema(t, locale)} />
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
              <BoardPreview
                gameId={game.id}
                cols={game.cols}
                cells={game.preview}
              />
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

      <footer className="site-footer">
        <Link href={localizedHref('/about', locale)}>{t('about.title')}</Link>
        <span className="site-footer__sep" aria-hidden>
          ·
        </span>
        <Link
          href={localizedHref('/copyright', locale)}
          aria-label={t('copyright.title')}
        >
          {tf('site.footer.copyright', { year })}
        </Link>
      </footer>
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

function BoardPreview({
  gameId,
  cols,
  cells,
}: {
  gameId: string;
  cols: number;
  cells: readonly (string | null)[];
}) {
  // Decorative mid-game snapshot — purely cosmetic, aria-hidden, never
  // routed through any game module. Side-id → glyph mapping is per-game
  // knowledge the landing page is allowed to know (it already enumerates
  // each game by id/slug/name).
  return (
    <div
      aria-hidden
      className="game-card__preview"
      style={{ gridTemplateColumns: `repeat(${cols}, 1fr)` }}
    >
      {cells.map((side, i) => (
        <div key={i} className="game-card__preview-cell">
          <PreviewToken gameId={gameId} side={side} />
        </div>
      ))}
    </div>
  );
}

function PreviewToken({ gameId, side }: { gameId: string; side: string | null }) {
  if (side === null) return null;
  if (gameId === 'tictactoe') {
    return <span className="game-card__preview-glyph">{side === 'x' ? '✕' : '◯'}</span>;
  }
  if (gameId === 'connect4') {
    return <span className={`game-card__preview-disc game-card__preview-disc--c4-${side}`} />;
  }
  if (gameId === 'reversi') {
    return <span className={`game-card__preview-disc game-card__preview-disc--rv-${side}`} />;
  }
  if (gameId === 'seabattle') {
    // 's' ship cell, 'h' hit, 'm' miss — shape-coded like the live board.
    if (side === 's') return <span className="game-card__preview-sb game-card__preview-sb--ship" />;
    if (side === 'h') return <span className="game-card__preview-sb game-card__preview-sb--hit">✕</span>;
    if (side === 'm') return <span className="game-card__preview-sb game-card__preview-sb--miss" />;
    return null;
  }
  return null;
}
