import type { MetadataRoute } from 'next';
import { GAME_CATALOG } from '@playme/shared';
import { SITE_URL } from '@/lib/site';

// Public, indexable surface only (CLAUDE.md §7, docs/frontend.md §2):
// the landing page plus every per-game configure page. Room URLs are
// noindex,nofollow and excluded here.
//
// hreflang pairing is wired now so the Sprint 6 /en route split is a
// path-table swap, not a sitemap refactor. The `en` entries 404 until
// then; Google ignores broken alternates rather than indexing them.

function entriesFor(path: string) {
  return {
    ka: `${SITE_URL}${path}`,
    en: `${SITE_URL}/en${path}`,
    'x-default': `${SITE_URL}${path}`,
  };
}

export default function sitemap(): MetadataRoute.Sitemap {
  const now = new Date();
  return [
    {
      url: `${SITE_URL}/`,
      lastModified: now,
      changeFrequency: 'weekly',
      priority: 1,
      alternates: { languages: entriesFor('/') },
    },
    ...GAME_CATALOG.map((game) => ({
      url: `${SITE_URL}/play/${game.slug}`,
      lastModified: now,
      changeFrequency: 'monthly' as const,
      priority: 0.8,
      alternates: { languages: entriesFor(`/play/${game.slug}`) },
    })),
  ];
}
