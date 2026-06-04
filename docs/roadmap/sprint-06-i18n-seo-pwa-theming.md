# Sprint 6 — i18n + SEO + PWA + theming (~1–2 weeks)

- i18next + `ka.json` and `en.json`. Every visible string moves behind a key.
- SEO: Next.js metadata, canonical, hreflang, sitemap, robots, JSON-LD on landing and per-game pages.
- PWA: manifest, icons, install prompt, service worker for offline shell.
- Theming: `next-themes`, semantic tokens in `globals.css`, light/dark/system, FOUC-prevention script.
- Accessibility pass: WCAG AA contrast in both themes, Connect 4 disc/ring legibility, focus rings, keyboard navigation.

**Exit criteria:** Lighthouse green (perf, a11y, SEO, best practices) on landing in both locales and both themes.

**Status:** Shipped 2026-05-20. Lighthouse desktop on `next start`:

| URL | Theme | Perf | A11y | Best Practices | SEO |
|---|---|---:|---:|---:|---:|
| `/` (ka) | light | 98 | 100 | 92 | 100 |
| `/` (ka) | dark | 100 | 100 | 92 | 100 |
| `/en` | light | 100 | 100 | 96 | 100 |
| `/en` | dark | 100 | 100 | 96 | 100 |

All four categories ≥ 90 across both locales and both themes. The Best Practices delta on the ka pages (92 vs 96) traces to `errors-in-console` / `inspector-issues` audits — not investigated; if anyone wants to push everything to 100, that's the lead.

**Resolved 2026-06-05:** the console issue was Zod v4's `new Function` JIT probe tripping the strict CSP (caught and harmless, but reported). Fixed via `z.config({ jitless: true })` in #140 — production Lighthouse now reads **100/100/100/100**.
