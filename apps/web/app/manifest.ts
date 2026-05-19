import type { MetadataRoute } from 'next';
import { DEFAULT_LOCALE, t } from '@playme/shared';

// PWA manifest. Routed by Next.js at /manifest.webmanifest and linked
// from the HTML head automatically — no manual <link rel="manifest">
// in app/layout.tsx.
//
// theme_color is the brand accent, shown by Chrome on the address bar
// when the PWA is installed and by Android as the app-drawer tint.
// background_color is the splash background during the cold-launch
// frame before the document paints; we pick the light-mode `--bg`
// because most users see light mode at first launch (the theme toggle
// only resolves after JS runs, and the splash is pre-JS).

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: t('site.title'),
    short_name: 'PlayMe',
    description: t('site.tagline'),
    start_url: '/',
    scope: '/',
    display: 'standalone',
    orientation: 'portrait',
    background_color: '#fafafa',
    theme_color: '#2a6df4',
    lang: DEFAULT_LOCALE,
    icons: [
      {
        src: '/icon-192.png',
        sizes: '192x192',
        type: 'image/png',
        purpose: 'any',
      },
      {
        src: '/icon-512.png',
        sizes: '512x512',
        type: 'image/png',
        purpose: 'any',
      },
      {
        src: '/icon-maskable-512.png',
        sizes: '512x512',
        type: 'image/png',
        purpose: 'maskable',
      },
    ],
  };
}
