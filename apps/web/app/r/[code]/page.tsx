import { notFound } from 'next/navigation';
import type { Metadata } from 'next';
import { PlaymeClient, type RoomDto } from '@playme/shared';
import { ssrApiBase } from '@/lib/api-base';
import { RoomClient } from './room-client';

interface PageProps {
  params: Promise<{ code: string }>;
}

// CLAUDE.md §2.5: room URLs are private/ephemeral — noindex always.
export const metadata: Metadata = {
  robots: { index: false, follow: false },
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
  const { code } = await params;
  const room = await fetchRoomSsr(code);
  if (!room) {
    notFound();
  }

  return (
    <main className="container">
      <RoomClient initialRoom={room} />
    </main>
  );
}
