import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { t } from '@playme/shared';
import { AnalyticsBoot } from '@/lib/analytics-boot';
import './globals.css';

export const metadata: Metadata = {
  metadataBase: new URL('https://playme.ge'),
  title: 'PlayMe — Play casual games with a friend, no signup',
  description: t('site.tagline'),
  robots: { index: true, follow: true },
};

export default function RootLayout({ children }: { children: ReactNode }) {
  // Sprint 1 ships en-only; the html lang stays "en" until Sprint 6 wires
  // the ka/en switcher per CLAUDE.md §2.5 / §3.
  return (
    <html lang="en">
      <body>
        <AnalyticsBoot />
        {children}
      </body>
    </html>
  );
}
