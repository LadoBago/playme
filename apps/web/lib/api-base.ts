// Resolve the API base URL once.
// - `PLAYME_API_URL` is server-only (SSR). Defaults to localhost:5080 for
//   the dev `dotnet run` setup; in prod, set to the absolute API host.
// - `NEXT_PUBLIC_API_URL` is inlined into the client bundle at build
//   time. Defaults to **empty** so the browser talks to the API via
//   same-origin paths (`/api/*`, `/hubs/*`) and Next.js's `rewrites()`
//   proxies them to the API. Same-origin keeps the session cookie
//   first-party, which matters in Safari (ITP partitions cross-port
//   cookies). In prod, set this to the absolute API host (e.g.
//   `https://api.playme.ge`) since the proxy is dev-only.

/* global process */
const SSR_FALLBACK = 'http://localhost:5080';

export const ssrApiBase = (process.env.PLAYME_API_URL ?? SSR_FALLBACK).replace(/\/$/, '');

export const browserApiBase = (process.env.NEXT_PUBLIC_API_URL ?? '').replace(/\/$/, '');

export function hubUrl(): string {
  return `${browserApiBase}/hubs/room`;
}
