'use client';

import { ActiveRoomCard } from './active-room-card';

/**
 * Configure-page guard. The session cookie is single-slot per browser
 * (docs/security.md §4): creating a new room while one is live silently
 * overwrites it, and an abandoned `inProgress` opponent wins after the
 * grace. Informative, not blocking — overwriting is sometimes what the
 * host wants (a friend who never opened the link) — so it only names the
 * room and offers the way back.
 */
export function ActiveRoomNotice() {
  return <ActiveRoomCard titleKey="resume.guard.title" noteKey="resume.guard.body" />;
}
