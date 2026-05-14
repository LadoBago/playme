// Public site URL (canonical, OG, sitemap, robots). Production points at
// playme.ge; preview deployments and local dev fall back to localhost so
// generated absolute URLs stay valid. Set NEXT_PUBLIC_SITE_URL on the
// Vercel project to switch this per environment (preview/prod).

/* global process */
const FALLBACK = 'http://localhost:3000';

export const SITE_URL = (process.env.NEXT_PUBLIC_SITE_URL ?? FALLBACK).replace(
  /\/$/,
  '',
);
