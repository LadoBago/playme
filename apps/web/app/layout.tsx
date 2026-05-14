import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';
import { DEFAULT_LOCALE, t } from '@playme/shared';
import { AnalyticsBoot } from '@/lib/analytics-boot';
import { SITE_URL } from '@/lib/site';
import './globals.css';

// Mobile viewport. Without this, iOS Safari renders the page at desktop
// width and amplifies its double-tap-to-zoom heuristic — two rapid taps
// on the same area (e.g. tapping the same Connect 4 column to drop a
// disc, then tapping again after the opponent's reply) get interpreted
// as a zoom gesture. `width=device-width, initial-scale=1` is the
// minimum that fixes that; `maximumScale=5` keeps pinch-zoom for a11y.
export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  maximumScale: 5,
};

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
