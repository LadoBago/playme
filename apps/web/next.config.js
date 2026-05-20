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
  // bundled with the route.
  outputFileTracingIncludes: {
    '/opengraph-image': ['./lib/fonts/*.woff'],
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
