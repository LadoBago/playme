/** @type {import('next').NextConfig} */
/* global process */

// Content-Security-Policy now lives in middleware.ts: it needs a fresh
// per-request nonce on `script-src`, which build-time headers() can't
// produce. The other security headers (XFO, XCTO, Referrer-Policy,
// HSTS, Permissions-Policy) stay here so they cover the static-asset
// paths the middleware matcher skips (/_next/static, the OG image,
// sitemap, etc.).

const ssrApiBase = process.env.PLAYME_API_URL ?? 'http://localhost:5080';

const nextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  // Transpile workspace TS packages from source — `@playme/shared`
  // exports `src/index.ts` directly, not a built `.js`.
  transpilePackages: ['@playme/shared'],
  // Same-origin proxy for the API in dev. Without this, the browser
  // sees localhost:3000 and localhost:5080 as different origins, and
  // Safari's ITP partitions the session cookie set on :5080 — leading
  // to the host being mis-detected as a challenger on the /r/<code>
  // page. Proxying through :3000 keeps the cookie first-party.
  async rewrites() {
    return [
      { source: '/api/:path*', destination: `${ssrApiBase}/api/:path*` },
      { source: '/hubs/:path*', destination: `${ssrApiBase}/hubs/:path*` },
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
