import type { MetadataRoute } from 'next';
import { GAME_CATALOG } from '@playme/shared';
import { SITE_URL } from '@/lib/site';

// Public, indexable surface only (CLAUDE.md §7, docs/frontend.md §2):
// the landing page plus every per-game configure page. Room URLs are
// noindex,nofollow and excluded here.
//
// hreflang pairs both locales: ka is served unprefixed (`/`, `/play/…`)
// and en under `/en`, both resolved by proxy.ts's locale rewrite — so
// every alternate below is a live URL. SITE_URL is the canonical apex
// (`playme.ge`); `www.playme.ge` 308-redirects to it.
//
// Each page is emitted ONCE PER LOCALE so both the ka and en URLs appear
// as their own <loc>. Google only queues a URL for crawl/index when it is
// a <loc>; hreflang xhtml:link alternates are a clustering hint, not a
// discovery directive — so listing only the ka <loc> with en as a mere
// alternate (as we did before) left every /en URL undiscovered. The
// alternate set is identical across the pair and self-referencing, per
// Google's hreflang-sitemap requirement.

function languagesFor(path: string) {
  // For the home page (`/`) the en variant is `/en`, not `/en/` — the app
  // runs with Next's default trailingSlash:false, so `/en/` would 308 to
  // `/en` and the <loc> would point at a redirect. Match the page's own
  // self-canonical (`/en`) exactly.
  const enPath = path === '/' ? '/en' : `/en${path}`;
  return {
    ka: `${SITE_URL}${path}`,
    en: `${SITE_URL}${enPath}`,
    'x-default': `${SITE_URL}${path}`,
  };
}

export default function sitemap(): MetadataRoute.Sitemap {
  const now = new Date();
  const paths = [
    '/',
    ...GAME_CATALOG.map((game) => `/play/${game.slug}`),
    '/about',
    '/copyright',
  ];

  return paths.flatMap((path) => {
    const languages = languagesFor(path);
    const isHome = path === '/';
    // The about + copyright pages are near-static informational pages —
    // listed so both locale variants are discoverable, but ranked below
    // the home and game pages.
    const isAbout = path === '/about';
    const isCopyright = path === '/copyright';
    const shared = {
      lastModified: now,
      changeFrequency: isCopyright
        ? ('yearly' as const)
        : isHome
          ? ('weekly' as const)
          : ('monthly' as const),
      priority: isCopyright ? 0.3 : isAbout ? 0.5 : isHome ? 1 : 0.8,
      alternates: { languages },
    };
    return [
      { url: languages.ka, ...shared },
      { url: languages.en, ...shared },
    ];
  });
}
