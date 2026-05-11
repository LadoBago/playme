import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import './globals.css';

export const metadata: Metadata = {
  metadataBase: new URL('https://playme.ge'),
  title: 'PlayMe — Play casual games with a friend, no signup',
  description:
    'Anonymous, real-time, two-player casual games: Tic-Tac-Toe and Connect 4. Create a room, share the link, play.',
  robots: { index: true, follow: true },
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="ka">
      <body>{children}</body>
    </html>
  );
}
