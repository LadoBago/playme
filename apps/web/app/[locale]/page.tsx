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
  return null;
}
