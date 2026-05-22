/** @type {import('next').NextConfig} */
/* global process */

// Content-Security-Policy now lives in middleware.ts: it needs a fresh
// per-request nonce on `script-src`, which build-time headers() can't
// produce. The other security headers (XFO, XCTO, Referrer-Policy,
// HSTS, Permissions-Policy) stay here so they cover the static-asset
// paths the middleware matcher skips (/_next/static, the OG image,
// sitemap, etc.).

// Upstream the API actually lives at. In prod, SSR fetches use this
// directly (server-to-server, fastest path). In local dev it's the API
// on :5080. The path-based rewrites below also use it as their target.
// Production browser traffic does NOT go through Vercel — it reaches
// api.playme.ge via Cloudflare directly.
const apiUpstream = process.env.PLAYME_API_URL ?? 'http://localhost:5080';

const nextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  // Transpile workspace TS packages from source — `@playme/shared`
  // exports `src/index.ts` directly, not a built `.js`.
  transpilePackages: ['@playme/shared'],
  // The OG image route reads vendored Noto Sans Georgian woff files
  // (app/opengraph-image.tsx) at runtime. Next.js's static tracer
  // doesn't follow the path.join(process.cwd(), 'lib/fonts/…') string,
  // so on Vercel the files would be stripped from the serverless
  // bundle and the runtime read would 500. This include keeps them
  // bundled with the route. The bracket segment is Next.js's
  // internal name for the `generateImageMetadata` variant id, so
  // both /opengraph-image/ka and /opengraph-image/en match.
  outputFileTracingIncludes: {
    '/opengraph-image/[__metadata_id__]': ['./lib/fonts/*.woff'],
  },
  // Same-origin proxy for the API in local dev. Without this, the browser
  // sees :3000 and :5080 as different origins, and Safari's ITP partitions
  // the session cookie set on :5080. Proxying through :3000 keeps the
  // cookie first-party.
  //
  // In production these rules are inert: the browser is configured (via
  // NEXT_PUBLIC_API_URL=https://api.playme.ge) to call the API on its own
  // subdomain, served by Cloudflare → Azure App Service. Vercel only ever
  // sees the www origin.
  async rewrites() {
    return [
      { source: '/api/:path*', destination: `${apiUpstream}/api/:path*` },
      { source: '/hubs/:path*', destination: `${apiUpstream}/hubs/:path*` },
    ];
  },
  // Sprint 9 PR2: legacy per-size Tic-Tac-Toe slugs 301 to the unified
  // configure page with the size pre-selected via `?size=N`. Keeps invite
  // links sitting in chats working through the 2-week redirect window.
  // PR3 of the sprint removes these rules + the legacy renderer entries.
  async redirects() {
    return [
      // Default locale (ka) at the root.
      { source: '/play/tictactoe-3x3', destination: '/play/tictactoe?size=3', permanent: true },
      { source: '/play/tictactoe-6x6', destination: '/play/tictactoe?size=6', permanent: true },
      { source: '/play/tictactoe-9x9', destination: '/play/tictactoe?size=9', permanent: true },
      // English locale prefix.
      { source: '/en/play/tictactoe-3x3', destination: '/en/play/tictactoe?size=3', permanent: true },
      { source: '/en/play/tictactoe-6x6', destination: '/en/play/tictactoe?size=6', permanent: true },
      { source: '/en/play/tictactoe-9x9', destination: '/en/play/tictactoe?size=9', permanent: true },
    ];
  },
  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          { key: 'X-Content-Type-Options', value: 'nosniff' },
          { key: 'X-Frame-Options', value: 'DENY' },
          { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
          {
            key: 'Strict-Transport-Security',
            value: 'max-age=63072000; includeSubDomains; preload',
          },
          {
            key: 'Permissions-Policy',
            value:
              'camera=(), microphone=(), geolocation=(), usb=(), payment=(), accelerometer=(), magnetometer=()',
          },
        ],
      },
    ];
  },
};

export default nextConfig;
