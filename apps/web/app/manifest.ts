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
    // Explicit id pins the app's identity to '/' regardless of any
    // future start_url change (renaming the homepage path would
    // otherwise look like a different app to the browser, splitting
    // install state across users' devices).
    id: '/',
    name: t('site.titleShort'),
    short_name: 'PlayMe',
    description: t('site.tagline'),
    start_url: '/',
    scope: '/',
    display: 'standalone',
    orientation: 'portrait',
    background_color: '#FFF4E6',
    theme_color: '#E54B1C',
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
    // Screenshots feed Chrome's richer install dialog. One per
    // form_factor is the minimum to unlock the wide-card UI on
    // desktop and the carousel UI on mobile. 2× DPI is fine — Chrome
    // scales them down for the prompt; sizes here must match the
    // actual file pixels or the browser drops them silently.
    screenshots: [
      {
        src: '/screenshot-wide.png',
        sizes: '2560x1440',
        type: 'image/png',
        form_factor: 'wide',
        label: 'PlayMe landing page',
      },
      {
        src: '/screenshot-narrow.png',
        sizes: '1440x2560',
        type: 'image/png',
        form_factor: 'narrow',
        label: 'PlayMe landing page',
      },
    ],
  };
}
