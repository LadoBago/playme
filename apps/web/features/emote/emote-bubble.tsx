'use client';

import { useEffect, useState, type CSSProperties } from 'react';
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

/**
 * How long a received emote stays on screen before it's removed. Kept in sync
 * with the CSS `emote-bubble-out` fade timing (delay + duration must total
 * this) so the bubble finishes fading exactly as the component unmounts it.
 */
const VISIBLE_MS = 3_500;

/** Number of sparks in the burst ring fired when the bubble pops in. */
const SPARK_COUNT = 6;
/** How far (in rem) each spark travels out from the bubble centre. */
const SPARK_RADIUS_REM = 1.15;

/**
 * Pre-computed burst vectors for the sparkle ring — even spokes around the
 * bubble starting from straight up. Static (module scope) so they don't
 * recompute on every render; the CSS reads each `--dx`/`--dy` to fan a spark
 * out in its own direction.
 */
const SPARKS: ReadonlyArray<{ dx: string; dy: string }> = Array.from(
  { length: SPARK_COUNT },
  (_, i) => {
    const angle = (i / SPARK_COUNT) * Math.PI * 2 - Math.PI / 2;
    return {
      dx: `${(Math.cos(angle) * SPARK_RADIUS_REM).toFixed(3)}rem`,
      dy: `${(Math.sin(angle) * SPARK_RADIUS_REM).toFixed(3)}rem`,
    };
  },
);

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
      data-emote={shown.emoteId}
      role="status"
      aria-label={t(`match.emote.${shown.emoteId}`)}
    >
      <EmoteIcon id={shown.emoteId} colorful />
      {SPARKS.map((spark, i) => (
        <span
          key={i}
          className="emote-bubble__spark"
          aria-hidden="true"
          style={{ '--dx': spark.dx, '--dy': spark.dy } as CSSProperties}
        />
      ))}
    </span>
  );
}
