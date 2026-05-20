import type { CSSProperties } from 'react';

/**
 * `playme.ge` wordmark. Renders inline text styled to match the brand
 * kit's `wordmark.svg` (Inter, mixed weights, accent `.ge`). Using text
 * rather than an SVG `<img>` means the wordmark themes automatically
 * (light/dark) via CSS tokens and the brand name remains selectable
 * and crawlable text in the DOM.
 *
 * The `as` prop lets a caller mount the wordmark inside a heading
 * (`<h1>`) without nesting block elements — the wordmark itself stays
 * inline-flex.
 */
interface WordmarkProps {
  size?: number | string;
  style?: CSSProperties;
}

export function Wordmark({ size = '3rem', style }: WordmarkProps) {
  return (
    <span
      className="wordmark"
      aria-hidden="true"
      style={{ fontSize: size, ...style }}
    >
      <span className="wordmark__play">play</span>
      <span className="wordmark__me">me</span>
      <span className="wordmark__ge">.ge</span>
    </span>
  );
}
