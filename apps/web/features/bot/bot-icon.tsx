/**
 * The bot glyph: a small, friendly robot drawn as inline SVG so it
 * inherits the theme via `currentColor` (no network fetch, no emoji
 * font drift). Decorative on its own — callers that need the icon to
 * mean something attach their own accessible text (see `BotBadge`).
 */
export function BotIcon() {
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
      <path d="M12 6V4" />
      <circle cx="12" cy="3" r="1" fill="currentColor" stroke="none" />
      <rect x="4" y="6" width="16" height="14" rx="3.5" />
      <path d="M2 11v4M22 11v4" />
      <circle cx="9" cy="11.5" r="1.1" fill="currentColor" stroke="none" />
      <circle cx="15" cy="11.5" r="1.1" fill="currentColor" stroke="none" />
      <path d="M9 15.3c0.9 1.1 1.9 1.6 3 1.6s2.1-.5 3-1.6" />
    </svg>
  );
}
