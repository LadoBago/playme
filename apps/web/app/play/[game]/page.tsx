import { notFound } from 'next/navigation';
import { findGame, t } from '@playme/shared';
import { ConfigureForm } from './configure-form';

interface PageProps {
  params: Promise<{ game: string }>;
}

export async function generateMetadata({ params }: PageProps) {
  const { game: slug } = await params;
  const game = findGame(slug);
  if (!game) return { title: 'PlayMe' };
  const name = t(game.nameKey as 'games.tictactoe-3x3.name');
  return {
    title: `Play ${name} online with a friend — PlayMe`,
    description: t(game.shortDescriptionKey as 'games.tictactoe-3x3.shortDescription'),
  };
}

export default async function ConfigurePage({ params }: PageProps) {
  const { game: slug } = await params;
  const game = findGame(slug);
  if (!game) {
    notFound();
  }

  const name = t(game.nameKey as 'games.tictactoe-3x3.name');
  const rules = t(game.rulesKey as 'games.tictactoe-3x3.rules');

  return (
    <main className="container stack" style={{ gap: '2rem' }}>
      <header className="stack" style={{ gap: '0.25rem' }}>
        <h1 style={{ fontSize: '1.75rem' }}>{name}</h1>
        <p style={{ color: 'var(--fg-muted)', margin: 0 }}>
          {t(game.shortDescriptionKey as 'games.tictactoe-3x3.shortDescription')}
        </p>
      </header>

      <div className="configure-grid">
        <section className="card stack">
          <h2 style={{ fontSize: '1.1rem' }}>{t('configure.title')}</h2>
          <ConfigureForm
            gameId={game.id}
            sides={game.sides.map((s) => ({ id: s.id, label: t(s.labelKey as 'games.tictactoe.sideX') }))}
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
