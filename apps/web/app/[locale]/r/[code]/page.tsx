import { notFound } from 'next/navigation';
import type { Metadata } from 'next';
import { PlaymeClient, type RoomDto, localeFromString } from '@playme/shared';
import { ssrApiBase } from '@/lib/api-base';
import { RoomClient } from './room-client';

// The [locale] segment in the URL is what flips translations; this
// page itself doesn't need to thread the locale value through — the
// client tree below picks it up via useTranslator() (which reads
// useParams().locale). We still validate it server-side so a bogus
// `/de/r/<code>` 404s instead of falling through to the catch-all.

interface PageProps {
  params: Promise<{ locale: string; code: string }>;
}

// CLAUDE.md §2.5: room URLs are private/ephemeral — noindex always.
// `alternates` overrides the root layout's inherited canonical + hreflang
// with nothing: a noindex page declaring a homepage canonical (or language
// alternates) is contradictory signalling to crawlers.
export const metadata: Metadata = {
  robots: { index: false, follow: false },
  alternates: {},
};

async function fetchRoomSsr(code: string): Promise<RoomDto | null> {
  const client = new PlaymeClient({ baseUrl: ssrApiBase });
  // SSR fetch — no cookie forwarded; we use it for the initial paint only
  // (room shape, status, players). Auth / role detection happens client-
  // side once SignalR connects.
  const res = await client.getRoom(code, { cache: 'no-store' });
  return res.ok ? res.value : null;
}

export default async function RoomPage({ params }: PageProps) {
  const { locale: localeRaw, code } = await params;
  if (!localeFromString(localeRaw)) notFound();
  const room = await fetchRoomSsr(code);
  if (!room) notFound();

  return (
    <main className="container">
      <RoomClient initialRoom={room} />
    </main>
  );
}
