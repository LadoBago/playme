// Renders one or more Schema.org nodes as a single `<script
// type="application/ld+json">` block. Build the node objects with the
// helpers in `@/lib/structured-data` and pass them here.
//
// CSP: the app runs a nonce + 'strict-dynamic' policy (proxy.ts). We
// deliberately do NOT attach a nonce here. A `type="application/ld+json"`
// block is a data island: the HTML "prepare the script" algorithm bails
// out for non-JS types before any CSP check runs, so `script-src` never
// governs it and no nonce is required. Adding one only caused a hydration
// mismatch — browsers blank the `nonce` content attribute in the DOM after
// parsing, so React reads back `nonce=""` and disagrees with the SSR HTML.
//
// `dangerouslySetInnerHTML` is required for JSON-LD — there is no React
// API for raw text in a <script>. The payload is built entirely from our
// own catalog + i18n strings (never request input); we still escape `<`
// to `<` so a stray sequence could never close the <script> tag
// early or inject markup.

interface JsonLdProps {
  data: Record<string, unknown> | Record<string, unknown>[];
}

export function JsonLd({ data }: JsonLdProps) {
  const json = JSON.stringify(data).replace(/</g, '\\u003c');
  return (
    <script
      type="application/ld+json"
      dangerouslySetInnerHTML={{ __html: json }}
    />
  );
}
