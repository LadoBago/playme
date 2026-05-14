/** @type {import('next').NextConfig} */

// Baseline Content-Security-Policy (docs/security.md §6). Notes:
// - `'unsafe-inline'` on `script-src` is a temporary concession to
//   Next.js's hydration metadata until nonce-based CSP lands; the rest
//   of the policy is strict enough to be defensive in depth.
// - `connect-src` includes wss:/ws: so the SignalR client can reach the
//   API origin in both dev (ws://localhost:5001) and prod (wss).
// - Sentry + PostHog endpoints are explicitly allowlisted because both
//   ship in the public bundle and need to call out to their backends.
const CSP = [
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline' https://*.sentry.io https://*.ingest.sentry.io https://*.posthog.com https://*.i.posthog.com",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob:",
  "font-src 'self' data:",
  "connect-src 'self' https://*.sentry.io https://*.ingest.sentry.io https://*.posthog.com https://*.i.posthog.com wss: ws:",
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
