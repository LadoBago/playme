import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { DEFAULT_LOCALE, t } from '@playme/shared';
import { AnalyticsBoot } from '@/lib/analytics-boot';
import { SITE_URL } from '@/lib/site';
import './globals.css';

// Public-page SEO surface (CLAUDE.md §7, docs/frontend.md §2). Per-page
// `metadata` exports inherit this and override the fields they care
// about. hreflang alternates land with the /en route split in Sprint 6.
export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: {
    default: t('site.title'),
    template: `%s ${t('site.titleSuffix')}`,
  },
  description: t('site.tagline'),
  applicationName: 'PlayMe',
  robots: { index: true, follow: true },
  alternates: { canonical: '/' },
  openGraph: {
    type: 'website',
    siteName: 'PlayMe',
    title: t('site.title'),
    description: t('site.tagline'),
    url: '/',
    locale: DEFAULT_LOCALE,
  },
  twitter: {
    card: 'summary_large_image',
    title: t('site.title'),
    description: t('site.tagline'),
  },
};

export default function RootLayout({ children }: { children: ReactNode }) {
  // Sprint 1 ships en-only; the /en route split + `ka` as default land in
  // Sprint 6 (CLAUDE.md §2.5, docs/frontend.md §2). The lang literal
  // tracks DEFAULT_LOCALE so flipping it is a one-line change.
  return (
    <html lang={DEFAULT_LOCALE}>
      <body>
        <AnalyticsBoot />
        {children}
      </body>
    </html>
  );
}
