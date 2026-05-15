import { ImageResponse } from 'next/og';
import { DEFAULT_LOCALE, t } from '@playme/shared';

/**
 * Public-page Open Graph image (docs/frontend.md §2). Next.js's file-
 * route convention auto-injects this as `og:image` for every page under
 * `/`, so the root layout and every per-game `/play/<game>` page get a
 * branded preview without per-page metadata.
 *
 * Generated dynamically via `ImageResponse` so the brand wordmark stays
 * in code (no binary asset to commit / regenerate when copy changes).
 * Edge runtime for a fast cold-start; the response is cached by Vercel
 * at the edge.
 */
export const runtime = 'edge';
export const size = { width: 1200, height: 630 };
export const contentType = 'image/png';
export const alt = 'PlayMe';

export default function Image() {
  return new ImageResponse(
    (
      <div
        style={{
          width: '100%',
          height: '100%',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          background: 'linear-gradient(135deg, #0e0e10 0%, #1a1a2e 100%)',
          color: '#ececec',
          padding: '64px',
          fontFamily: 'system-ui, -apple-system, sans-serif',
        }}
      >
        <div
          style={{
            fontSize: 144,
            fontWeight: 700,
            letterSpacing: '-0.04em',
            color: '#6ea8ff',
            lineHeight: 1,
          }}
        >
          PlayMe
        </div>
        <div
          style={{
            marginTop: 32,
            fontSize: 40,
            fontWeight: 400,
            textAlign: 'center',
            maxWidth: 900,
            color: '#a0a0a0',
            lineHeight: 1.25,
          }}
        >
          {t('site.tagline', DEFAULT_LOCALE)}
        </div>
      </div>
    ),
    { ...size },
  );
}
