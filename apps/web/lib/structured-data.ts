import {
  type GameCatalogEntry,
  type I18nKey,
  type Locale,
  localizedHref,
} from '@playme/shared';
import { SITE_URL } from './site';

// Builders for Schema.org JSON-LD (https://schema.org), emitted on the
// public, indexable pages (home + per-game configure pages) to earn
// richer search results. Pure functions returning plain objects — the
// `<JsonLd>` component serializes them. Room pages stay noindex and get
// no structured data.
//
// Human-readable fields (`name`, `description`) flow through the i18n
// translator so both locales stay in sync. The remaining values are
// Schema.org *vocabulary* — controlled enums like `playMode`, or
// machine descriptors like `gamePlatform` — not on-screen UI copy, so
// they are fixed literals by design, not i18n keys.

type Translate = (key: I18nKey) => string;

/** Absolute, locale-aware URL for a site-relative path. */
function absoluteUrl(path: string, locale: Locale): string {
  return `${SITE_URL}${localizedHref(path, locale)}`;
}

const schemaLanguage = (locale: Locale): string => (locale === 'ka' ? 'ka-GE' : 'en-US');

/**
 * `WebSite` node for the landing page — anchors the brand and its
 * canonical URL for the search engine's knowledge of the site.
 */
export function buildWebSiteSchema(t: Translate, locale: Locale): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'WebSite',
    name: 'PlayMe',
    url: absoluteUrl('/', locale),
    description: t('site.tagline'),
    inLanguage: schemaLanguage(locale),
  };
}

/**
 * `VideoGame` node for a per-game configure page. PlayMe games are
 * two-player, real-time, browser-based — encoded here so the page can
 * qualify for game-specific result treatments.
 */
export function buildVideoGameSchema(
  game: GameCatalogEntry,
  t: Translate,
  locale: Locale,
): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'VideoGame',
    name: t(game.nameKey),
    url: absoluteUrl(`/play/${game.slug}`, locale),
    description: t(game.metaDescriptionKey),
    inLanguage: schemaLanguage(locale),
    // Schema.org vocabulary (not localized UI): exactly two players,
    // multiplayer, played in any web browser.
    playMode: 'MultiPlayer',
    numberOfPlayers: {
      '@type': 'QuantitativeValue',
      value: 2,
    },
    gamePlatform: 'Web browser',
    applicationCategory: 'Game',
    publisher: {
      '@type': 'Organization',
      name: 'PlayMe',
      url: SITE_URL,
    },
  };
}

/**
 * `BreadcrumbList` (Home → Game) for a per-game page, so results can
 * render the breadcrumb trail instead of a bare URL.
 */
export function buildBreadcrumbSchema(
  game: GameCatalogEntry,
  t: Translate,
  locale: Locale,
): Record<string, unknown> {
  return {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: [
      {
        '@type': 'ListItem',
        position: 1,
        // Brand name, not localized UI copy.
        name: 'PlayMe',
        item: absoluteUrl('/', locale),
      },
      {
        '@type': 'ListItem',
        position: 2,
        name: t(game.nameKey),
        item: absoluteUrl(`/play/${game.slug}`, locale),
      },
    ],
  };
}
