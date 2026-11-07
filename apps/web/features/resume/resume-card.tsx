'use client';

import { ActiveRoomCard } from './active-room-card';

/**
 * "Your game is still on" card for the landing page — the way back after
 * an accidental tab close. SSR renders nothing, so the indexable markup
 * and the Lighthouse lab run never see it.
 */
export function ResumeCard() {
  return <ActiveRoomCard titleKey="resume.title" />;
}
