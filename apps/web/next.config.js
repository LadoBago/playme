/** @type {import('next').NextConfig} */
/* global process */

// Content-Security-Policy now lives in middleware.ts: it needs a fresh
// per-request nonce on `script-src`, which build-time headers() can't
// produce. The other security headers (XFO, XCTO, Referrer-Policy,
// HSTS, Permissions-Policy) stay here so they cover the static-asset
// paths the middleware matcher skips (/_next/static, the OG image,
// sitemap, etc.).

// Upstream the API actually lives at (Azure App Service in prod, the
// local API on :5080 in dev). Used for both SSR fetches and as the
// destination of the same-origin rewrites below.
const apiUpstream = process.env.PLAYME_API_URL ?? 'http://localhost:5080';

// Optional: a hostname (e.g. `api.playme.ge`) that Vercel claims as a
// project domain. When set, ALL requests to that hostname are rewritten
// to `apiUpstream`. Lets the API be reached at its branded subdomain with
// TLS terminated by Vercel — needed when the origin can't serve a valid
// cert for the subdomain itself (we hit this with Azure App Service
// Managed Certificates silently failing on the `.ge` TLD). Unset in dev.
const apiProxyHost = process.env.PLAYME_API_PROXY_HOST ?? '';

const nextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  // Transpile workspace TS packages from source — `@playme/shared`
  // exports `src/index.ts` directly, not a built `.js`.
  transpilePackages: ['@playme/shared'],
  // Same-origin proxies for the API. Two complementary rules:
  //   1. Path-based (/api/*, /hubs/*) — used in dev so the browser hits
  //      :3000 instead of :5080, avoiding Safari ITP partitioning the
  //      session cookie. Also a viable prod mode if you point
  //      NEXT_PUBLIC_API_URL at the web origin itself.
  //   2. Host-based (api.playme.ge → upstream) — used in prod so the API
  //      is reachable at its intended subdomain with first-party TLS
  //      served by Vercel. Skipped when PLAYME_API_PROXY_HOST is unset.
  async rewrites() {
    const rules = [
      { source: '/api/:path*', destination: `${apiUpstream}/api/:path*` },
      { source: '/hubs/:path*', destination: `${apiUpstream}/hubs/:path*` },
    ];

    if (apiProxyHost) {
      rules.push({
        source: '/:path*',
        has: [{ type: 'host', value: apiProxyHost }],
        destination: `${apiUpstream}/:path*`,
      });
    }

    return rules;
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
