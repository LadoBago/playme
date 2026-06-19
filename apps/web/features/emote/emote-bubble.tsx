'use client';

import { useEffect, useState } from 'react';
import type { EmoteId } from '@playme/shared';
import { useTranslator } from '@/lib/use-locale';
import { EmoteIcon } from './emote-icon';

/**
 * The latest received emote plus a `nonce` that changes on every receive —
 * so repeating the same emote still restarts the animation and resets the
 * auto-dismiss timer.
 */
export interface IncomingEmote {
  emoteId: EmoteId;
  nonce: number;
}

/** How long a received emote stays on screen before fading out. */
const VISIBLE_MS = 2_400;

/**
 * Transient bubble for an emote the opponent sent. Anchored over the
 * opponent's clock face (the parent positions it); fades itself out after
 * {@link VISIBLE_MS}. Carries no interactivity — purely a notification.
 */
export function EmoteBubble({ emote }: { emote: IncomingEmote | null }) {
  const { t } = useTranslator();
  const [shown, setShown] = useState<IncomingEmote | null>(null);

  useEffect(() => {
    if (!emote) return undefined;
    setShown(emote);
    const id = setTimeout(() => setShown(null), VISIBLE_MS);
    return () => clearTimeout(id);
  }, [emote]);

  if (!shown) return null;

  return (
    <span
      // Remount on each nonce so the pop animation replays for repeats.
      key={shown.nonce}
      className="emote-bubble"
      role="status"
      aria-label={t(`match.emote.${shown.emoteId}`)}
    >
      <EmoteIcon id={shown.emoteId} />
    </span>
  );
}
