import type { EmoteId } from '@playme/shared';

/**
 * The emote glyphs, drawn as inline SVG so they inherit the theme via
 * `currentColor` (consistent across macOS/Windows/Android, unlike system
 * emoji) and need no network fetch. Keyed by the shared {@link EmoteId}
 * allowlist; the picker and the incoming bubble both render through here.
 *
 * With {@link EmoteIconProps.colorful} the glyph paints itself in its own
 * signature hue (filled — a solid symbol, or a tinted face). The hue is a
 * theme token resolved in CSS from the `data-emote` attribute, so no color
 * is hard-coded here (per CLAUDE.md §6/§7). Off, it renders as a plain
 * `currentColor` outline exactly as before.
 */
interface EmoteIconProps {
  id: EmoteId;
  /** Paint the glyph in its own emote color, filled, instead of an outline. */
  colorful?: boolean;
}

export function EmoteIcon({ id, colorful = false }: EmoteIconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      width="100%"
      height="100%"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.7"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={colorful ? 'emote-icon emote-icon--colorful' : 'emote-icon'}
      data-emote={id}
      aria-hidden="true"
      focusable="false"
    >
      {glyph(id, colorful)}
    </svg>
  );
}

// Clap glyph adapted from the uxwing "clap" icon (free license), rendered as
// an outline. Authored in a 117.57 × 122.88 space, so the clap case scales it
// into the shared 24×24 viewBox with a compensating stroke width.
const CLAP_PATH =
  'M113.6,74.1c-0.77-1.4-1.91-2.47-3.22-3.18l1.88-1.03c1.92-1.06,3.23-2.8,3.8-4.75c0.57-1.96,0.39-4.13-0.66-6.05c0,0,0-0.01,0-0.01c-0.78-1.42-1.99-2.57-3.44-3.28l1.51-0.83c1.92-1.06,3.23-2.8,3.8-4.75c0.57-1.96,0.39-4.13-0.66-6.05c-1.05-1.92-2.8-3.23-4.75-3.79c-1.96-0.57-4.13-0.39-6.05,0.66l-1.54,0.85c0.15-1.53-0.15-3.12-0.94-4.57c-1.05-1.92-2.8-3.23-4.75-3.8c-1.96-0.57-4.13-0.39-6.05,0.66l-7.92,4.36c-0.85-1.86-2.32-3.45-4.3-4.41c0,0-0.01,0-0.01,0c-1.68-0.81-3.56-1.09-5.4-0.8l0.87-1.78c1.1-2.26,1.18-4.76,0.41-6.97c-0.76-2.21-2.37-4.13-4.63-5.23c-2.26-1.1-4.76-1.18-6.97-0.41c-2.21,0.76-4.13,2.37-5.23,4.62l-0.88,1.81c-0.88-1.53-2.21-2.83-3.91-3.66c-2.26-1.1-4.76-1.18-6.97-0.41c-2.21,0.76-4.13,2.37-5.23,4.63L23.36,64.92c-0.43,0.63-0.82,1.29-1.16,1.98l0,0l-1.12,2.29l-0.11-0.29c-0.77-2.01-1.47-4.07-1.74-4.84c-1.79-5.21-5.2-7.79-8.65-8.29c-1.56-0.23-3.1-0.03-4.51,0.53c-1.41,0.56-2.68,1.49-3.7,2.73c-2.41,2.93-3.37,7.63-1.04,13.32l0.03,0.1l0,0l0.01,0.02c2.47,7.27,8.27,29.5,14.65,36.5c7.89,8.66,28.31,16.78,39.3,12.91c6.06-2.13,11.32-6.59,14.36-12.83l1.13-2.32c13.22-7.27,26.43-14.55,39.66-21.83c1.92-1.05,3.23-2.8,3.79-4.75C114.83,78.2,114.66,76.02,113.6,74.1L113.6,74.1L113.6,74.1z M85.43,42.19c2.93-1.6,5.88-3.21,8.83-4.84c1.04-0.57,2.23-0.67,3.3-0.36c1.07,0.31,2.02,1.02,2.59,2.07c0.57,1.04,0.67,2.23,0.36,3.3c-0.31,1.07-1.02,2.02-2.07,2.59l-8.98,4.94c-0.29-0.18-0.58-0.35-0.9-0.5l0,0c-1.65-0.8-3.43-1.06-5.13-0.84l1.08-2.21C85.16,45,85.45,43.58,85.43,42.19L85.43,42.19z M92.22,52.64l15.32-8.43c1.04-0.57,2.23-0.67,3.3-0.36c1.07,0.31,2.02,1.02,2.59,2.07c0.57,1.04,0.67,2.23,0.36,3.3c-0.31,1.07-1.02,2.02-2.07,2.59L92.35,62.46l0.42-0.87c1.1-2.26,1.18-4.76,0.41-6.97C92.95,53.93,92.62,53.26,92.22,52.64L92.22,52.64z M89.45,68.42l16.88-9.29c1.04-0.57,2.23-0.67,3.3-0.36c1.07,0.31,2.02,1.02,2.59,2.07c0.57,1.04,0.67,2.23,0.36,3.3c-0.31,1.07-1.02,2.02-2.07,2.59L87.73,79.26l1.74,3.17l15.07-8.29c1.04-0.57,2.23-0.67,3.3-0.36c1.07,0.31,2.02,1.02,2.59,2.07c0.57,1.04,0.67,2.23,0.36,3.3c-0.31,1.07-1.02,2.02-2.07,2.59C97,88.19,85.28,94.64,73.56,101.09L89.45,68.42L89.45,68.42z M65.95,107.24c-2.55,5.23-6.94,8.97-12.01,10.75c-9.36,3.3-28.17-4.45-34.87-11.8C13.38,99.95,7.11,76.52,5.29,71.15c-0.02-0.09-0.06-0.19-0.09-0.28l-1.92,0.79l1.91-0.79c-1.7-4.1-1.16-7.32,0.4-9.21c0.57-0.69,1.26-1.2,2.02-1.5c0.76-0.3,1.57-0.41,2.38-0.29c2.04,0.3,4.12,2.01,5.34,5.54c0.28,0.81,1.02,2.96,1.79,4.97c0.72,1.89,1.51,3.75,2.16,4.69c0.2,0.35,0.51,0.63,0.88,0.81c0,0,0,0,0,0l1.83,0l4.02-7.14C32.68,55,39.36,41.52,46.08,27.73c0.6-1.23,1.65-2.1,2.86-2.52c1.21-0.42,2.57-0.38,3.8,0.22c1.23,0.6,2.1,1.65,2.52,2.85c0.42,1.21,0.38,2.57-0.22,3.8L42.01,58.85l3.86,1.88l17.21-35.36c0.6-1.23,1.65-2.1,2.85-2.52c1.21-0.42,2.57-0.38,3.8,0.22c1.23,0.6,2.1,1.65,2.52,2.86c0.42,1.21,0.38,2.57-0.22,3.8L54.82,65.09l3.94,1.92l13.07-26.85c0.6-1.23,1.65-2.1,2.86-2.52c1.21-0.42,2.57-0.38,3.8,0.22c1.23,0.6,2.1,1.65,2.52,2.86c0.42,1.21,0.38,2.57-0.22,3.8L67.71,71.36l3.73,1.82l8.64-17.76c0.6-1.23,1.64-2.1,2.85-2.52c1.21-0.42,2.57-0.38,3.8,0.22c1.23,0.6,2.1,1.65,2.52,2.86c0.41,1.21,0.38,2.57-0.22,3.8C81.34,75.6,73.65,91.42,65.95,107.24L65.95,107.24z M82.16,14.53c-0.01,1.36-1.12,2.46-2.48,2.44c-1.36-0.01-2.46-1.12-2.44-2.48L77.3,2.58c0.01-1.36,1.12-2.46,2.48-2.44c1.36,0.01,2.46,1.12,2.44,2.48L82.16,14.53L82.16,14.53z M95.14,14.37c-0.54,1.25-2,1.82-3.25,1.28c-1.25-0.54-1.82-2-1.28-3.25l4.78-10.91c0.54-1.25,2-1.82,3.25-1.28c1.25,0.54,1.82,2,1.28,3.25L95.14,14.37L95.14,14.37z M100,26.05c-1.11,0.79-2.65,0.54-3.44-0.57c-0.79-1.11-0.54-2.65,0.57-3.44l10.99-7.88c1.11-0.79,2.65-0.54,3.44,0.57c0.79,1.11,0.54,2.65-0.57,3.44L100,26.05L100,26.05z';

// Face disc: a light tint of the emote color when colorful (eyes/mouth stay
// full-strength on top), a plain outline otherwise.
function faceOutline(colorful: boolean): React.ReactNode {
  return (
    <circle
      cx="12"
      cy="12"
      r="9.2"
      fill={colorful ? 'currentColor' : 'none'}
      fillOpacity={colorful ? 0.18 : undefined}
    />
  );
}
const Eyes = (
  <>
    <circle cx="8.8" cy="10" r="0.9" fill="currentColor" stroke="none" />
    <circle cx="15.2" cy="10" r="0.9" fill="currentColor" stroke="none" />
  </>
);

// A switch (not an indexed lookup) keeps this off the object-injection sink
// the linter flags for dynamic property access. `colorful` fills the symbol
// glyphs (heart/like/clap/poke) solid and tints the faces; off, everything
// stays a plain outline.
function glyph(id: EmoteId, colorful: boolean): React.ReactNode {
  // Solid fill for the symbol glyphs when colorful; outline otherwise.
  const fill = colorful ? 'currentColor' : 'none';
  switch (id) {
    case 'smile':
      return (
        <>
          {faceOutline(colorful)}
          {Eyes}
          <path d="M8.2 14c1 1.5 2.3 2.2 3.8 2.2s2.8-.7 3.8-2.2" />
        </>
      );
    case 'heart':
      return (
        <path
          fill={fill}
          d="M12 20.3l-1.5-1.37C5.4 14.3 2.5 11.6 2.5 8.1 2.5 5.3 4.7 3 7.5 3c1.7 0 3.3.8 4.5 2.1C13.2 3.8 14.8 3 16.5 3 19.3 3 21.5 5.3 21.5 8.1c0 3.5-2.9 6.2-8 10.83L12 20.3z"
        />
      );
    case 'like':
      return (
        <>
          <path fill={fill} d="M7 11v9.5H4.2a1.2 1.2 0 0 1-1.2-1.2v-7.1a1.2 1.2 0 0 1 1.2-1.2H7z" />
          <path
            fill={fill}
            d="M7 11l3.7-7.2a1.7 1.7 0 0 1 3.2 1l-.7 3.9h4.9a1.9 1.9 0 0 1 1.87 2.27l-1.1 6a1.9 1.9 0 0 1-1.87 1.53H7"
          />
        </>
      );
    case 'cry':
      return (
        <>
          {faceOutline(colorful)}
          {Eyes}
          <path d="M8.4 16.2c1-1.2 2.2-1.8 3.6-1.8s2.6.6 3.6 1.8" />
          <path
            fill={fill}
            d="M8.8 12c-.9 1.3-1.5 2.3-1.5 3a1.5 1.5 0 0 0 3 0c0-.7-.6-1.7-1.5-3z"
          />
        </>
      );
    case 'clap':
      // uxwing clap outline, scaled from its native 117.57×122.88 space into
      // 24×24; strokeWidth is pre-divided by the scale so it lands at ~1.7.
      return (
        <g transform="translate(2 1.5) scale(0.17)" strokeWidth={10}>
          <path fill={fill} d={CLAP_PATH} />
        </g>
      );
    case 'poke':
      // A hand pointing right: index finger extended sideways, the thumb
      // folded over the top, and the other three fingers curled into the
      // fist below it (the three knuckle bumps on the front edge). Two
      // concentric arcs in front of the fingertip read as a poke/jab.
      // Poke stays an outline even when colorful — the filled hand reads
      // poorly, so it just takes the emote hue on its stroke.
      return (
        <>
          <path d="M6 10C6 7.5 7.2 6.4 8.7 6.4 10 6.4 10.6 7.6 11 8.8H19.5a1.5 1.5 0 0 1 0 3H12.7a1.15 1.15 0 0 1 0 2.3 1.15 1.15 0 0 1 0 2.3 1.15 1.15 0 0 1 0 2.3Q12.7 20 10.5 20H8Q6 20 6 17.7Z" />
          <path d="M22.2 9.3a1.6 1.6 0 0 1 0 2.4M23 8.7a2 2 0 0 1 0 3.2" />
        </>
      );
  }
}
