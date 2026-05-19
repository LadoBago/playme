import type { Metadata, Viewport } from 'next';
import { headers } from 'next/headers';
import type { ReactNode } from 'react';
import { DEFAULT_LOCALE, t } from '@playme/shared';
import { themeFoucScript } from '@/features/theme/fouc-script';
import { AnalyticsBoot } from '@/lib/analytics-boot';
import { SITE_URL } from '@/lib/site';
import './globals.css';

// Force per-request rendering so middleware's nonce CSP can attach a
// fresh nonce to every framework script tag (see middleware.ts). Without
// this, statically prerendered pages keep their build-time HTML — script
// tags emitted at build time carry no nonce, but the CSP requires one
// under 'strict-dynamic', so the browser blocks them.
//
// Vercel CDN-caches dynamic responses with the right cache-control, so
// the LCP cost is small. /opengraph-image, /sitemap.xml, /robots.txt
// are file-route handlers outside this layout and stay static.
export const dynamic = 'force-dynamic';

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
// about. The hreflang alternates are wired now so the Sprint 6 /en
// route split is a URL-table swap, not a metadata refactor; the `en`
// entries 404 until that route lands (Google ignores broken
// alternates) and the source of truth becomes a single map.
export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: {
    default: t('site.title'),
    template: `%s ${t('site.titleSuffix')}`,
  },
  description: t('site.tagline'),
  applicationName: 'PlayMe',
  robots: { index: true, follow: true },
  alternates: {
    canonical: '/',
    languages: {
      ka: '/',
      en: '/en',
      'x-default': '/',
    },
  },
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

export default async function RootLayout({ children }: { children: ReactNode }) {
  // Default locale is ka (CLAUDE.md §1). The /en route split lands in
  // Sprint 6; until then DEFAULT_LOCALE governs both <html lang> and the
  // OG locale tag.
  //
  // `suppressHydrationWarning` on <html> covers the data-theme attribute
  // that the FOUC script writes before React hydrates. The script's
  // payload is a compile-time string literal (features/theme/fouc-
  // script.ts), so it's safe to inline; the per-request CSP nonce from
  // middleware.ts is attached so 'strict-dynamic' admits it.
  //
  // suppressHydrationWarning on the <script> itself covers a separate,
  // expected mismatch: browsers strip the `nonce` attribute from the
  // DOM after parse (the value lives on the internal IDL property for
  // CSP enforcement only). The server-rendered tree carries the nonce;
  // the post-parse DOM reports it as "". The script has already
  // executed by the time React hydrates, so the difference is purely
  // cosmetic — but loud in the console without this flag.
  const nonce = (await headers()).get('x-nonce') ?? undefined;

  return (
    <html lang={DEFAULT_LOCALE} suppressHydrationWarning>
      <body>
        <script
          nonce={nonce}
          suppressHydrationWarning
          dangerouslySetInnerHTML={{ __html: themeFoucScript }}
        />
        <AnalyticsBoot />
        {children}
      </body>
    </html>
  );
}
