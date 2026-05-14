import type { MetadataRoute } from 'next';
import { GAME_CATALOG } from '@playme/shared';
import { SITE_URL } from '@/lib/site';

// Public, indexable surface only (CLAUDE.md §7, docs/frontend.md §2):
// the landing page plus every per-game configure page. Room URLs are
// noindex,nofollow per CLAUDE.md and excluded here. The /en split + the
// hreflang `alternates` map land in Sprint 6 once the locale routing is
// wired.
export default function sitemap(): MetadataRoute.Sitemap {
  const now = new Date();
  return [
    {
      url: `${SITE_URL}/`,
      lastModified: now,
      changeFrequency: 'weekly',
      priority: 1,
    },
    ...GAME_CATALOG.map((game) => ({
      url: `${SITE_URL}/play/${game.slug}`,
      lastModified: now,
      changeFrequency: 'monthly' as const,
      priority: 0.8,
    })),
  ];
}
