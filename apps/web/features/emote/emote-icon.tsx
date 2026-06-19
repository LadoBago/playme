import type { EmoteId } from '@playme/shared';

/**
 * The emote glyphs, drawn as inline SVG so they inherit the theme via
 * `currentColor` (consistent across macOS/Windows/Android, unlike system
 * emoji) and need no network fetch. Keyed by the shared {@link EmoteId}
 * allowlist; the picker and the incoming bubble both render through here.
 */
export function EmoteIcon({ id }: { id: EmoteId }) {
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
      aria-hidden="true"
      focusable="false"
    >
      {glyph(id)}
    </svg>
  );
}

const FaceOutline = <circle cx="12" cy="12" r="9.2" />;
const Eyes = (
  <>
    <circle cx="8.8" cy="10" r="0.9" fill="currentColor" stroke="none" />
    <circle cx="15.2" cy="10" r="0.9" fill="currentColor" stroke="none" />
  </>
);

// A switch (not an indexed lookup) keeps this off the object-injection sink
// the linter flags for dynamic property access.
function glyph(id: EmoteId): React.ReactNode {
  switch (id) {
    case 'smile':
      return (
        <>
          {FaceOutline}
          {Eyes}
          <path d="M8.2 14c1 1.5 2.3 2.2 3.8 2.2s2.8-.7 3.8-2.2" />
        </>
      );
    case 'heart':
      return (
        <path
          d="M12 20.3l-1.5-1.37C5.4 14.3 2.5 11.6 2.5 8.1 2.5 5.3 4.7 3 7.5 3c1.7 0 3.3.8 4.5 2.1C13.2 3.8 14.8 3 16.5 3 19.3 3 21.5 5.3 21.5 8.1c0 3.5-2.9 6.2-8 10.83L12 20.3z"
          fill="currentColor"
          stroke="none"
        />
      );
    case 'wow':
      return (
        <>
          {FaceOutline}
          {Eyes}
          <circle cx="12" cy="15" r="1.9" />
        </>
      );
    case 'cry':
      return (
        <>
          {FaceOutline}
          {Eyes}
          <path d="M8.4 16.2c1-1.2 2.2-1.8 3.6-1.8s2.6.6 3.6 1.8" />
          <path
            d="M8.8 12c-.9 1.3-1.5 2.3-1.5 3a1.5 1.5 0 0 0 3 0c0-.7-.6-1.7-1.5-3z"
            fill="currentColor"
            stroke="none"
          />
        </>
      );
    case 'clap':
      return (
        <>
          <rect x="6.2" y="10" width="5.4" height="10.4" rx="2.7" transform="rotate(-13 8.9 15.2)" />
          <rect x="12.4" y="10" width="5.4" height="10.4" rx="2.7" transform="rotate(13 15.1 15.2)" />
          <path d="M12 3.2v2.1M8.1 4.3l1 1.8M15.9 4.3l-1 1.8" />
        </>
      );
    case 'poke':
      return (
        <>
          <path d="M10.5 12V5.6a1.6 1.6 0 0 1 3.2 0V12" />
          <path d="M13.7 9.4a1.5 1.5 0 0 1 3 0V13" />
          <path d="M16.7 10.6a1.5 1.5 0 0 1 3 0v3.2a6.5 6.5 0 0 1-6.5 6.5h-1.2a6 6 0 0 1-4.4-1.9l-3-3.2a1.6 1.6 0 0 1 2.3-2.2l1.8 1.7V10" />
        </>
      );
  }
}
