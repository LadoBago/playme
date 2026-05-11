// Next.js config. The web hits the API directly via `NEXT_PUBLIC_API_URL`
// (browser-side absolute URL) and `PLAYME_API_URL` (SSR absolute URL).
// CORS on the API allows http://localhost:3000 with credentials so the
// signed session cookie (CLAUDE.md §5.4) rides on every cross-origin
// request — both ports are same-site under localhost, so SameSite=Lax
// is sufficient.

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // Transpile workspace TS packages from source — `@playme/shared`
  // exports `src/index.ts` directly, not a built `.js`.
  transpilePackages: ['@playme/shared'],
};

export default nextConfig;
