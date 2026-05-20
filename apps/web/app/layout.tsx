import type { Metadata, Viewport } from 'next';
import { headers } from 'next/headers';
import { Inter } from 'next/font/google';
import type { ReactNode } from 'react';
import { createTranslator } from '@playme/shared';
import { LangSync } from '@/features/locale/lang-sync';
import { LocaleToggle } from '@/features/locale/locale-toggle';
import { InstallPromptInit } from '@/features/pwa/install-prompt-init';
import { ServiceWorkerRegister } from '@/features/pwa/sw-register';
import { themeFoucScript } from '@/features/theme/fouc-script';
import { ThemeToggle } from '@/features/theme/theme-toggle';
import { AnalyticsBoot } from '@/lib/analytics-boot';
import { getServerLocale } from '@/lib/locale';
import { SITE_URL } from '@/lib/site';
import './globals.css';

// Brand wordmark targets Inter (play 400 / me 700 / .ge 500 per the
// brand kit). next/font self-hosts the woff2 from /_next/static/media,
// so the CSP `font-src 'self'` directive in middleware.ts already
// covers it — no header change needed. Georgian glyphs fall through
// to system fonts (Inter doesn't ship a Georgian subset); that's
// intentional and renders correctly via the font-family fallback
// chain in globals.css.
const inter = Inter({
  subsets: ['latin', 'latin-ext'],
  weight: ['400', '500', '700'],
  variable: '--font-inter',
  display: 'swap',
});

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
  // Browser chrome (Chrome / Safari address bar) tracks the page
  // background per scheme, so the toolbar blends with the page
  // instead of competing with it. The PWA manifest's theme_color
  // stays brand-accent — that's only seen on the installed app
  // splash / launcher, where brand recognition matters more.
  themeColor: [
    { media: '(prefers-color-scheme: light)', color: '#FFF4E6' },
    { media: '(prefers-color-scheme: dark)', color: '#1a0f0a' },
  ],
};

// Public-page SEO surface (CLAUDE.md §7, docs/frontend.md §2). Per-page
// metadata inherits this and overrides what it cares about. The locale
// flips here based on the middleware-supplied x-locale header — both
// the human-readable strings and the og:locale tag track it.
export async function generateMetadata(): Promise<Metadata> {
  const locale = await getServerLocale();
  const { t } = createTranslator(locale);
  const canonical = locale === 'ka' ? '/' : '/en';
  return {
    metadataBase: new URL(SITE_URL),
    title: {
      default: t('site.title'),
      template: `%s ${t('site.titleSuffix')}`,
    },
    description: t('site.tagline'),
    applicationName: 'PlayMe',
    robots: { index: true, follow: true },
    alternates: {
      canonical,
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
      url: canonical,
      locale: locale === 'ka' ? 'ka_GE' : 'en_US',
      // Explicit URL pins which of the two file-route variants
      // (app/opengraph-image.tsx → generateImageMetadata returns
      // [ka, en]) this page emits. Without this, Next.js would
      // auto-inject one og:image meta tag per variant on every
      // page, so a Georgian page would advertise both the Georgian
      // and English image. Pointing at the locale-matched URL
      // collapses that to a single tag.
      images: [
        {
          url: `/opengraph-image/${locale}`,
          width: 1200,
          height: 630,
          alt: t('site.ogImageAlt'),
        },
      ],
    },
    twitter: {
      card: 'summary_large_image',
      title: t('site.title'),
      description: t('site.tagline'),
      // Twitter falls back to og:image when twitter:image is absent
      // but Next.js otherwise emits twitter:image entries for every
      // file-convention variant too — same de-duplication concern.
      images: [
        {
          url: `/opengraph-image/${locale}`,
          alt: t('site.ogImageAlt'),
        },
      ],
    },
  };
}

export default async function RootLayout({ children }: { children: ReactNode }) {
  // The locale and CSP nonce both ride in on request headers set by
  // middleware.ts. We need them at the root so <html lang> matches the
  // route and the inline theme-FOUC script carries the per-request
  // nonce required by 'strict-dynamic'.
  //
  // suppressHydrationWarning on <html> covers the data-theme attribute
  // that the theme FOUC script writes before React hydrates.
  //
  // suppressHydrationWarning on the <script> covers a separate, expected
  // mismatch: browsers strip the `nonce` attribute from the DOM after
  // parse (the value lives on the internal IDL property for CSP
  // enforcement only). The server-rendered tree carries the nonce; the
  // post-parse DOM reports it as "".
  const requestHeaders = await headers();
  const nonce = requestHeaders.get('x-nonce') ?? undefined;
  const locale = await getServerLocale();

  return (
    <html lang={locale} className={inter.variable} suppressHydrationWarning>
      <body>
        <script
          nonce={nonce}
          suppressHydrationWarning
          dangerouslySetInnerHTML={{ __html: themeFoucScript }}
        />
        <AnalyticsBoot />
        <ServiceWorkerRegister />
        <InstallPromptInit />
        <LangSync />
        <div className="toolbar">
          <LocaleToggle />
          <ThemeToggle />
        </div>
        {children}
      </body>
    </html>
  );
}
