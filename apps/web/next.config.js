/** @type {import('next').NextConfig} */

// Baseline Content-Security-Policy (docs/security.md §6). Notes:
// - `'unsafe-inline'` on `script-src` is a temporary concession to
//   Next.js's hydration metadata until nonce-based CSP lands; the rest
//   of the policy is strict enough to be defensive in depth.
// - `'unsafe-eval'` is added in dev only — React's dev build uses
//   `eval()` for callstack reconstruction and Turbopack uses it for
//   module evaluation; React production builds never call `eval()`, so
//   prod CSP stays free of it.
// - `connect-src` allowlists the API origin (HTTP + WebSocket) derived
//   from NEXT_PUBLIC_API_URL so the typed client + SignalR can reach
//   the API. Sentry + PostHog endpoints are listed explicitly because
//   both ship in the public bundle and need to call out to their
//   backends.
const isDev = process.env.NODE_ENV !== 'production';

// In dev, web on :3000 proxies /api/* and /hubs/* to the API at
// PLAYME_API_URL (default http://localhost:5080) via Next.js rewrites
// below. Browser-side requests stay same-origin, which keeps the
// session cookie first-party and avoids Safari ITP cookie partitioning
// (the symptom that made room-creation fall back to JoinForm on Safari).
const ssrApiBase = process.env.PLAYME_API_URL ?? 'http://localhost:5080';

// For connect-src: if NEXT_PUBLIC_API_URL is set (prod cross-origin
// setup) allow that origin explicitly; otherwise everything goes
// through 'self' via the rewrites and the extra entry is unneeded.
const browserApiBase = process.env.NEXT_PUBLIC_API_URL;
let apiOrigin = '';
let apiWsOrigin = '';
if (browserApiBase && /^https?:\/\//.test(browserApiBase)) {
  apiOrigin = new URL(browserApiBase).origin;
  apiWsOrigin = apiOrigin.replace(/^http/, 'ws');
}

const scriptSrc = [
  "'self'",
  "'unsafe-inline'",
  isDev ? "'unsafe-eval'" : '',
  'https://*.sentry.io',
  'https://*.ingest.sentry.io',
  'https://*.posthog.com',
  'https://*.i.posthog.com',
]
  .filter(Boolean)
  .join(' ');
const connectSrc = [
  "'self'",
  apiOrigin,
  apiWsOrigin,
  'https://*.sentry.io',
  'https://*.ingest.sentry.io',
  'https://*.posthog.com',
  'https://*.i.posthog.com',
]
  .filter(Boolean)
  .join(' ');
const CSP = [
  "default-src 'self'",
  `script-src ${scriptSrc}`,
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob:",
  "font-src 'self' data:",
  `connect-src ${connectSrc}`,
  "frame-ancestors 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "object-src 'none'",
].join('; ');

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
  // Security headers (docs/security.md §6).
  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          { key: 'Content-Security-Policy', value: CSP },
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
