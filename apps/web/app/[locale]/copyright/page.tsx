import Link from 'next/link';
import type { Metadata } from 'next';
import { createTranslator, localizedHref } from '@playme/shared';
import { resolveLocale } from '@/lib/locale';

// SSR + indexable (CLAUDE.md §2.5 SEO). A simple © notice page. Reachable
// at `/copyright` (ka) and `/en/copyright` (en) — both route through this
// [locale] segment. `metadataBase`, `robots`, and the title template are
// inherited from the root layout; this page overrides title, description,
// alternates, and re-declares openGraph/twitter in full (metadata objects
// overwrite — not deep-merge — across segments).

interface PageProps {
  params: Promise<{ locale: string }>;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const locale = await resolveLocale(params);
  const { t } = createTranslator(locale);
  const metaTitle = t('copyright.metaTitle');
  const description = t('copyright.metaDescription');
  const canonical = localizedHref('/copyright', locale);
  const image = {
    url: `/opengraph-image/${locale}`,
    alt: t('site.ogImageAlt'),
  };
  return {
    // Self-contained title (already names PlayMe) → `absolute` so the root
    // layout's "— PlayMe" suffix template isn't appended on top. The bare
    // "Copyright — PlayMe" was thin for an indexable, sitemapped page.
    title: { absolute: metaTitle },
    description,
    alternates: {
      canonical,
      languages: {
        ka: '/copyright',
        en: '/en/copyright',
        'x-default': '/copyright',
      },
    },
    openGraph: {
      type: 'website',
      siteName: 'PlayMe',
      title: metaTitle,
      description,
      url: canonical,
      locale: locale === 'ka' ? 'ka_GE' : 'en_US',
      images: [{ ...image, width: 1200, height: 630 }],
    },
    twitter: {
      card: 'summary_large_image',
      title: metaTitle,
      description,
      images: [image],
    },
  };
}

export default async function CopyrightPage({ params }: PageProps) {
  const locale = await resolveLocale(params);
  const { t, tf } = createTranslator(locale);
  // Rendered at request time (the root layout pins `dynamic = 'force-dynamic'`
  // for the whole subtree), so the year stays current without a redeploy.
  const year = new Date().getFullYear();

  return (
    <main className="container stack" style={{ gap: '1.5rem' }}>
      <h1 style={{ margin: 0 }}>{t('copyright.title')}</h1>
      <p style={{ color: 'var(--fg)', fontWeight: 500, margin: 0 }}>
        {tf('copyright.notice', { year })}
      </p>
      <p style={{ color: 'var(--fg-muted)', margin: 0, lineHeight: 1.6 }}>{t('copyright.body')}</p>
      <p style={{ margin: 0 }}>
        <Link href={localizedHref('/', locale)} style={{ color: 'var(--fg-muted)' }}>
          {t('notFound.home')}
        </Link>
      </p>
    </main>
  );
}
