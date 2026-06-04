import Link from 'next/link';
import type { Metadata } from 'next';
import { createTranslator, localizedHref } from '@playme/shared';
import { resolveLocale } from '@/lib/locale';

// SSR + indexable (CLAUDE.md §2.5 SEO). A short, personal "about this
// project" page. Reachable at `/about` (ka) and `/en/about` (en) — both
// route through this [locale] segment. `metadataBase`, `robots`, and the
// title template are inherited from the root layout; this page overrides
// title, description, alternates, and re-declares openGraph/twitter in
// full (metadata objects overwrite — not deep-merge — across segments).

interface PageProps {
  params: Promise<{ locale: string }>;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const locale = await resolveLocale(params);
  const { t } = createTranslator(locale);
  const description = t('about.metaDescription');
  const canonical = localizedHref('/about', locale);
  const image = {
    url: `/opengraph-image/${locale}`,
    alt: t('site.ogImageAlt'),
  };
  return {
    title: t('about.title'),
    description,
    alternates: {
      canonical,
      languages: {
        ka: '/about',
        en: '/en/about',
        'x-default': '/about',
      },
    },
    openGraph: {
      type: 'website',
      siteName: 'PlayMe',
      title: t('about.title'),
      description,
      url: canonical,
      locale: locale === 'ka' ? 'ka_GE' : 'en_US',
      images: [{ ...image, width: 1200, height: 630 }],
    },
    twitter: {
      card: 'summary_large_image',
      title: t('about.title'),
      description,
      images: [image],
    },
  };
}

export default async function AboutPage({ params }: PageProps) {
  const locale = await resolveLocale(params);
  const { t } = createTranslator(locale);

  return (
    <main className="container stack" style={{ gap: '1.5rem' }}>
      <h1 style={{ margin: 0 }}>{t('about.title')}</h1>
      <p style={{ color: 'var(--fg-muted)', margin: 0, lineHeight: 1.6 }}>
        {t('about.intro')}
      </p>
      <ul className="about-points">
        <Point label={t('about.why.label')} body={t('about.why.body')} />
        <Point label={t('about.how.label')} body={t('about.how.body')} />
        <Point label={t('about.stack.label')} body={t('about.stack.body')} />
      </ul>
      <p style={{ margin: 0 }}>
        <Link href={localizedHref('/', locale)} style={{ color: 'var(--fg-muted)' }}>
          {t('notFound.home')}
        </Link>
      </p>
    </main>
  );
}

function Point({ label, body }: { label: string; body: string }) {
  return (
    <li>
      <strong>{label}</strong>
      <span style={{ color: 'var(--fg-muted)' }}> — {body}</span>
    </li>
  );
}
