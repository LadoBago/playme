// Resolve the API base URL once. `PLAYME_API_URL` is server-only (SSR),
// `NEXT_PUBLIC_API_URL` is inlined into the client bundle at build time.
// Both default to localhost:5080 for the dev `dotnet run --project apps/api`
// setup.

/* global process */
const FALLBACK = 'http://localhost:5080';

export const ssrApiBase = (process.env.PLAYME_API_URL ?? FALLBACK).replace(/\/$/, '');

export const browserApiBase = (
  process.env.NEXT_PUBLIC_API_URL ?? FALLBACK
).replace(/\/$/, '');

export function hubUrl(): string {
  return `${browserApiBase}/hubs/room`;
}
