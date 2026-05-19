import { NextResponse, type NextRequest } from 'next/server';

/* global process, crypto, btoa, Headers */

/**
 * Per-request Content-Security-Policy with a nonce on `script-src`
 * (docs/security.md §6). In production this replaces the
 * `'unsafe-inline'` concession with `'nonce-<random>' 'strict-dynamic'`
 * — Next.js framework scripts read the nonce from the `x-nonce`
 * request header we set below and apply it to every inline / module
 * script the runtime emits, so the browser only executes scripts that
 * carry the matching nonce or were transitively loaded from one.
 *
 * In dev we keep `'unsafe-inline' 'unsafe-eval'`: Next dev's HMR
 * injects inline scripts without nonces, and React dev uses `eval()`
 * for callstack reconstruction. Both are off in production.
 *
 * Cost: pages that previously prerendered at build time become
 * dynamic, since the nonce can't be baked into static HTML. For the
 * /, /play/[game] surfaces that's fine — they're cheap to render and
 * Vercel's CDN caches the response anyway.
 */

const isDev = process.env.NODE_ENV !== 'production';

// Optional absolute API origin (prod deployments where the web and API
// live on different hosts). Empty / unset means same-origin via the
// rewrites in next.config.js — 'self' covers it.
const browserApiBase = process.env.NEXT_PUBLIC_API_URL;
let apiOrigin = '';
let apiWsOrigin = '';
if (browserApiBase && /^https?:\/\//.test(browserApiBase)) {
  apiOrigin = new URL(browserApiBase).origin;
  apiWsOrigin = apiOrigin.replace(/^http/, 'ws');
}

function generateNonce(): string {
  // Edge runtime: Buffer isn't available; btoa + Web Crypto are.
  // 16 bytes -> 22-24 chars base64, well above the 128-bit recommendation.
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  let binary = '';
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary);
}

function buildCsp(nonce: string): string {
  // PostHog dynamically loads supplemental scripts (config, autocapture,
  // web-vitals) from its asset host at runtime. In production
  // 'strict-dynamic' covers that because the PostHog SDK is loaded by a
  // nonce'd bundle and the descendant scripts inherit; in dev there's
  // no strict-dynamic, so the source-list mode requires explicit
  // allowlist entries. Sentry behaves the same way.
  const externalScriptHosts = [
    'https://*.sentry.io',
    'https://*.ingest.sentry.io',
    'https://*.posthog.com',
    'https://*.i.posthog.com',
  ];

  const scriptSrc = isDev
    ? // Next dev's HMR + React dev require unsafe-inline/eval; production
      // drops them. External hosts are needed because dev has no
      // 'strict-dynamic' to grant scripts transitively.
      ["'self'", "'unsafe-inline'", "'unsafe-eval'", ...externalScriptHosts]
    : // 'strict-dynamic' tells modern browsers to ignore the source-list
      // and trust only nonce'd scripts and what they transitively load.
      // 'self' + the external host allowlist are kept as a fallback for
      // older browsers that don't honour strict-dynamic.
      [
        "'self'",
        `'nonce-${nonce}'`,
        "'strict-dynamic'",
        ...externalScriptHosts,
      ];

  const connectSrc = [
    "'self'",
    apiOrigin,
    apiWsOrigin,
    'https://*.sentry.io',
    'https://*.ingest.sentry.io',
    'https://*.posthog.com',
    'https://*.i.posthog.com',
  ].filter(Boolean);

  return [
    "default-src 'self'",
    `script-src ${scriptSrc.join(' ')}`,
    // Style 'unsafe-inline' stays: React + Next emit inline styles for
    // hydration, and style XSS doesn't run JS. Tightening this would
    // require nonces on every inline <style> in every component.
    "style-src 'self' 'unsafe-inline'",
    "img-src 'self' data: blob:",
    "font-src 'self' data:",
    `connect-src ${connectSrc.join(' ')}`,
    "frame-ancestors 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "object-src 'none'",
  ].join('; ');
}

// Paths that live outside the [locale] segment in app/ — these are
// static assets or file-route handlers (manifest.ts / sw.js / icons /
// offline page). We still want CSP on them, but never the locale
// rewrite — rewriting /icon.svg to /ka/icon.svg would 404.
function isAssetPath(pathname: string): boolean {
  if (pathname === '/manifest.webmanifest') return true;
  if (pathname === '/icon.svg') return true;
  if (pathname === '/apple-icon.png') return true;
  if (pathname === '/offline.html') return true;
  if (pathname === '/sw.js') return true;
  if (pathname.startsWith('/icon-')) return true;
  if (pathname.startsWith('/screenshot-')) return true;
  return false;
}

// Map a request path to { locale, internalPath } for the [locale]
// segment routing. /en[...] keeps its prefix; everything else gets
// rewritten to /ka[...] so the file-system route resolves while the
// user's URL bar stays clean.
function resolveLocaleRewrite(pathname: string): {
  locale: 'ka' | 'en';
  internalPath: string;
} {
  if (pathname === '/en' || pathname.startsWith('/en/')) {
    return { locale: 'en', internalPath: pathname };
  }
  return {
    locale: 'ka',
    internalPath: pathname === '/' ? '/ka' : `/ka${pathname}`,
  };
}

export function middleware(request: NextRequest) {
  const nonce = generateNonce();
  const csp = buildCsp(nonce);
  const pathname = request.nextUrl.pathname;

  // Pass the nonce through to the page via the request headers. Next.js
  // looks up `x-nonce` and applies it to its framework-injected scripts
  // (hydration, RSC streaming) and to any <Script> that doesn't carry
  // its own nonce prop.
  const requestHeaders = new Headers(request.headers);
  requestHeaders.set('x-nonce', nonce);

  // Static assets — no rewrite, but still attach CSP.
  if (isAssetPath(pathname)) {
    const response = NextResponse.next({ request: { headers: requestHeaders } });
    response.headers.set('Content-Security-Policy', csp);
    return response;
  }

  const { locale, internalPath } = resolveLocaleRewrite(pathname);
  requestHeaders.set('x-locale', locale);

  let response;
  if (internalPath === pathname) {
    response = NextResponse.next({ request: { headers: requestHeaders } });
  } else {
    const url = request.nextUrl.clone();
    url.pathname = internalPath;
    response = NextResponse.rewrite(url, { request: { headers: requestHeaders } });
  }

  response.headers.set('Content-Security-Policy', csp);
  return response;
}

export const config = {
  // Skip middleware on paths that don't render HTML (API rewrites,
  // SignalR proxy, static assets, the OG image, robots/sitemap). RSC
  // prefetch requests are also skipped — they ride on the page's CSP.
  matcher: [
    {
      source:
        '/((?!api|hubs|_next/static|_next/image|opengraph-image|favicon.ico|robots.txt|sitemap.xml).*)',
      missing: [
        { type: 'header', key: 'next-router-prefetch' },
        { type: 'header', key: 'purpose', value: 'prefetch' },
      ],
    },
  ],
};
