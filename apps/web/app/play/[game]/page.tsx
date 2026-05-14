import { notFound } from 'next/navigation';
import type { Metadata } from 'next';
import { findGame, t } from '@playme/shared';
import { ConfigureForm } from './configure-form';

interface PageProps {
  params: Promise<{ game: string }>;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { game: slug } = await params;
  const game = findGame(slug);
  if (!game) return {};
  return {
    title: t(game.nameKey),
    description: t(game.shortDescriptionKey),
    alternates: { canonical: `/play/${game.slug}` },
    openGraph: {
      title: t(game.nameKey),
      description: t(game.shortDescriptionKey),
      url: `/play/${game.slug}`,
    },
    twitter: {
      title: t(game.nameKey),
      description: t(game.shortDescriptionKey),
    },
  };
}

export default async function ConfigurePage({ params }: PageProps) {
  const { game: slug } = await params;
  const game = findGame(slug);
  if (!game) {
    notFound();
  }

  const name = t(game.nameKey);
  const rules = t(game.rulesKey);

  return (
    <main className="container stack" style={{ gap: '2rem' }}>
      <header className="stack" style={{ gap: '0.25rem' }}>
        <h1 style={{ fontSize: '1.75rem' }}>{name}</h1>
        <p style={{ color: 'var(--fg-muted)', margin: 0 }}>
          {t(game.shortDescriptionKey)}
        </p>
      </header>

      <div className="configure-grid">
        <section className="card stack">
          <h2 style={{ fontSize: '1.1rem' }}>{t('configure.title')}</h2>
          <ConfigureForm
            gameId={game.id}
            sides={game.sides.map((s) => ({ id: s.id, label: t(s.labelKey) }))}
            defaultHostSide={game.defaultHostSide}
          />
        </section>

        <aside className="card stack">
          <h2 style={{ fontSize: '1.1rem' }}>{t('configure.rules.title')}</h2>
          <p className="rules-panel">{rules}</p>
        </aside>
      </div>
    </main>
  );
}
