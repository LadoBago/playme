import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { ImageResponse } from 'next/og';
import { createTranslator, type Locale } from '@playme/shared';

/**
 * Per-locale Open Graph images. `generateImageMetadata` produces two
 * variants — `/opengraph-image/ka` and `/opengraph-image/en` — and the
 * root layout's `generateMetadata` (app/layout.tsx) sets
 * `openGraph.images` explicitly to the variant matching the current
 * page's locale, so each rendered page emits exactly one `og:image`
 * meta tag pointing at the right locale.
 *
 * Both variant URLs sit under `/opengraph-image/*`, which the middle-
 * ware matcher already excludes (see middleware.ts) — no locale
 * rewrite runs, so no header plumbing is needed.
 *
 * Layout matches the brand kit's `og-image.svg`: cream bg, brand mark
 * on the left (rounded square + grid + two tokens), wordmark `play`
 * 400 / `me` 700 / `.ge` 500, brand tagline below in muted cocoa. The
 * mark is composed of positioned <div>s rather than inline SVG so
 * satori's HTML-CSS subset renders it deterministically.
 *
 * Runs on the Node.js runtime, not edge — the next/og bundle (satori
 * + resvg) is ~1.01 MB and Vercel's Hobby tier caps edge functions at
 * 1 MB. Node serverless functions are 50 MB. OG images are low-volume
 * and Vercel CDN-caches the response, so the extra cold-start is
 * irrelevant in practice.
 *
 * The Georgian tagline glyphs need a Georgian-capable font; satori's
 * default font is Latin-only. Noto Sans Georgian (woff 400/500,
 * ~20 KB per weight) is vendored under lib/fonts/ and bundled with
 * this route via `outputFileTracingIncludes` in next.config.js —
 * without that the files are stripped from the Vercel serverless
 * function and the runtime read errors. The English variant doesn't
 * need the font but loading it unconditionally keeps the code paths
 * symmetric and the cold-start cost is one fs read.
 */
const size = { width: 1200, height: 630 };
const contentType = 'image/png';

export function generateImageMetadata() {
  // Pull the alt strings from the i18n catalog so the tagline is
  // sourced once (per CLAUDE.md §6: "No hard-coded user-facing
  // strings — always through an i18n key"). The rendered image
  // pulls the same key from the same catalog, so the two stay in
  // lock-step.
  return [
    {
      id: 'ka',
      alt: `playme.ge — ${createTranslator('ka').t('site.brandTagline')}`,
      size,
      contentType,
    },
    {
      id: 'en',
      alt: `playme.ge — ${createTranslator('en').t('site.brandTagline')}`,
      size,
      contentType,
    },
  ];
}

async function loadGeorgianFonts() {
  // Paths are joined from process.cwd() with literal filenames; no
  // user input reaches `readFile`. The security rule's non-literal
  // detection is a false positive here.
  const fontDir = path.join(process.cwd(), 'lib', 'fonts');
  const [regular, medium] = await Promise.all([
    // eslint-disable-next-line security/detect-non-literal-fs-filename
    readFile(path.join(fontDir, 'NotoSansGeorgian-Regular.woff')),
    // eslint-disable-next-line security/detect-non-literal-fs-filename
    readFile(path.join(fontDir, 'NotoSansGeorgian-Medium.woff')),
  ]);
  return [
    {
      name: 'Noto Sans Georgian',
      data: regular,
      style: 'normal' as const,
      weight: 400 as const,
    },
    {
      name: 'Noto Sans Georgian',
      data: medium,
      style: 'normal' as const,
      weight: 500 as const,
    },
  ];
}

export default async function Image({ id }: { id: Promise<string> }) {
  // Next.js 16 wraps the metadata-route `id` in a Promise (same shape
  // as `params` for [locale]/page.tsx) — awaiting it yields the
  // variant string. The values are whatever `generateImageMetadata`
  // returned above; narrow defensively in case Next.js ever surfaces
  // an unknown id (we fall back to the default locale so the request
  // still resolves rather than 500-ing).
  const resolvedId = await id;
  const locale: Locale = resolvedId === 'en' ? 'en' : 'ka';
  const { t } = createTranslator(locale);
  const tagline = t('site.brandTagline');
  const fonts = await loadGeorgianFonts();

  return new ImageResponse(
    (
      <div
        style={{
          width: '100%',
          height: '100%',
          display: 'flex',
          alignItems: 'center',
          background: '#FFF4E6',
          padding: '0 120px',
          fontFamily:
            '"Inter", "Noto Sans Georgian", "Helvetica Neue", system-ui, sans-serif',
        }}
      >
        <div
          style={{
            width: 200,
            height: 200,
            borderRadius: 44,
            background: '#E54B1C',
            position: 'relative',
            display: 'flex',
            flexShrink: 0,
          }}
        >
          <div
            style={{
              position: 'absolute',
              top: 46,
              left: 98,
              width: 4,
              height: 108,
              background: '#FFFFFF',
              borderRadius: 2,
            }}
          />
          <div
            style={{
              position: 'absolute',
              top: 98,
              left: 46,
              width: 108,
              height: 4,
              background: '#FFFFFF',
              borderRadius: 2,
            }}
          />
          <div
            style={{
              position: 'absolute',
              top: 56,
              left: 56,
              width: 34,
              height: 34,
              borderRadius: 9999,
              background: '#FFFFFF',
            }}
          />
          <div
            style={{
              position: 'absolute',
              top: 110,
              left: 110,
              width: 34,
              height: 34,
              borderRadius: 9999,
              background: '#F59E0B',
            }}
          />
        </div>

        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            marginLeft: 56,
          }}
        >
          <div
            style={{
              display: 'flex',
              fontSize: 120,
              letterSpacing: '-3.5px',
              lineHeight: 1,
            }}
          >
            <span style={{ fontWeight: 400, color: '#1F1F1F' }}>play</span>
            <span style={{ fontWeight: 700, color: '#1F1F1F' }}>me</span>
            <span style={{ fontWeight: 500, color: '#E54B1C' }}>.ge</span>
          </div>
          <div
            style={{
              marginTop: 28,
              fontSize: 40,
              fontWeight: 500,
              color: '#6B5848',
              letterSpacing: '-0.5px',
              fontFamily:
                '"Noto Sans Georgian", "Inter", "Helvetica Neue", system-ui, sans-serif',
            }}
          >
            {tagline}
          </div>
        </div>
      </div>
    ),
    { ...size, fonts },
  );
}
