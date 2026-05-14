import type { MetadataRoute } from 'next';
import { SITE_URL } from '@/lib/site';

// Crawler policy (CLAUDE.md §7, docs/frontend.md §2):
//   - public surfaces (landing, per-game configure) are crawlable;
//   - /r/<roomCode> and anything below it is private/ephemeral — disallow
//     in addition to the per-page noindex on those routes (defense in
//     depth: a crawler that ignores meta robots still respects this);
//   - /dev/* is local-only scaffolding, also kept out of the index.
export default function robots(): MetadataRoute.Robots {
  return {
    rules: [
      {
        userAgent: '*',
        allow: '/',
        disallow: ['/r/', '/dev/'],
      },
    ],
    sitemap: `${SITE_URL}/sitemap.xml`,
    host: SITE_URL,
  };
}
