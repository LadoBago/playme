import { headers } from 'next/headers';

// Renders one or more Schema.org nodes as a single `<script
// type="application/ld+json">` block. Build the node objects with the
// helpers in `@/lib/structured-data` and pass them here.
//
// CSP: the app runs a nonce + 'strict-dynamic' policy (middleware.ts).
// A JSON-LD block is a data island, not executable script, but we attach
// the per-request nonce anyway so the tag is unambiguously allowed under
// the strict policy and stays consistent with the FOUC script in the
// root layout.
//
// `dangerouslySetInnerHTML` is required for JSON-LD — there is no React
// API for raw text in a <script>. The payload is built entirely from our
// own catalog + i18n strings (never request input); we still escape `<`
// to `<` so a stray sequence could never close the <script> tag
// early or inject markup.

interface JsonLdProps {
  data: Record<string, unknown> | Record<string, unknown>[];
}

export async function JsonLd({ data }: JsonLdProps) {
  const nonce = (await headers()).get('x-nonce') ?? undefined;
  const json = JSON.stringify(data).replace(/</g, '\\u003c');
  return (
    <script
      type="application/ld+json"
      nonce={nonce}
      dangerouslySetInnerHTML={{ __html: json }}
    />
  );
}
