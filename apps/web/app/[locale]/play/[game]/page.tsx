import Link from 'next/link';
import { notFound } from 'next/navigation';
import type { Metadata } from 'next';
import {
  createTranslator,
  findGame,
  localeFromString,
  localizedHref,
} from '@playme/shared';
import { ConfigureForm } from './configure-form';

interface PageProps {
  params: Promise<{ locale: string; game: string }>;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { locale: localeRaw, game: slug } = await params;
  const locale = localeFromString(localeRaw);
  if (!locale) return {};
  const game = findGame(slug);
  if (!game) return {};

  const { t } = createTranslator(locale);
  const path = `/play/${game.slug}`;
  const kaPath = localizedHref(path, 'ka');
  const enPath = localizedHref(path, 'en');
  const canonical = localizedHref(path, locale);
  // SEO-only strings (metaTitle / metaDescription) carry search synonyms;
  // the on-screen <h1> and subheading still render nameKey /
  // shortDescriptionKey. `title.absolute` bypasses the root layout's
  // `%s — PlayMe` suffix template since metaTitle already ends in "| PlayMe".
  return {
    title: { absolute: t(game.metaTitleKey) },
    description: t(game.metaDescriptionKey),
    robots: { index: true, follow: true },
    alternates: {
      canonical,
      languages: {
        ka: kaPath,
        en: enPath,
        'x-default': kaPath,
      },
    },
    openGraph: {
      type: 'website',
      siteName: 'PlayMe',
      // OG title stays the clean game name — social cards read better
      // without the keyword-stuffed document title.
      title: t(game.nameKey),
      description: t(game.metaDescriptionKey),
      url: canonical,
      locale: locale === 'ka' ? 'ka_GE' : 'en_US',
    },
    twitter: {
      card: 'summary_large_image',
      title: t(game.nameKey),
      description: t(game.metaDescriptionKey),
    },
  };
}

export default async function ConfigurePage({ params }: PageProps) {
  const { locale: localeRaw, game: slug } = await params;
  const locale = localeFromString(localeRaw);
  if (!locale) notFound();
  const game = findGame(slug);
  if (!game) notFound();

  const { t } = createTranslator(locale);
  const name = t(game.nameKey);
  const rules = t(game.rulesKey);

  return (
    <main className="container stack" style={{ gap: '2rem' }}>
      <Link
        href={localizedHref('/', locale)}
        className="icon-link"
        aria-label={t('configure.back')}
        style={{ alignSelf: 'flex-start' }}
      >
        <svg
          width="20"
          height="20"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
          focusable="false"
        >
          <path d="M19 12H5M12 19l-7-7 7-7" />
        </svg>
      </Link>

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
