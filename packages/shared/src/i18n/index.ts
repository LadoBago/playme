import { en, type EnKey } from './en';
import { ka } from './ka';

export type Locale = 'en' | 'ka';
export type I18nKey = EnKey;

const catalogs: Record<Locale, Record<EnKey, string>> = {
  en,
  ka,
};

export const DEFAULT_LOCALE: Locale = 'ka';

/**
 * Translate a key for the given locale. Falls back to en, then to the
 * raw key. Sprint 6 swaps this for the full i18next pipeline with
 * pluralization, interpolation, and runtime catalog loading.
 */
export function t(key: I18nKey, locale: Locale = DEFAULT_LOCALE): string {
  // Both locale and key are statically typed to literal unions; the
  // generic object-injection check can't see that and trips a false
  // positive on these lookups.
  /* eslint-disable security/detect-object-injection */
  const catalog = catalogs[locale] ?? catalogs[DEFAULT_LOCALE];
  return catalog[key] ?? catalogs.en[key] ?? key;
  /* eslint-enable security/detect-object-injection */
}

/**
 * Translate `key` and interpolate `{name}` placeholders from `params`.
 * Unknown placeholders are left in place (loud bug rather than a silent
 * empty string). Sprint 6's i18next swap accepts the same call shape.
 */
export function tf(
  key: I18nKey,
  params: Readonly<Record<string, string | number>>,
  locale: Locale = DEFAULT_LOCALE,
): string {
  const template = t(key, locale);
  // The captured group is `\w+`, so no shell/HTML metacharacters can
  // smuggle through; the indexed property lookup is also bounded by the
  // template's own placeholders, never by user input.
  /* eslint-disable security/detect-object-injection */
  return template.replace(/\{(\w+)\}/g, (match, name: string) => {
    const v = params[name];
    return v === undefined ? match : String(v);
  });
  /* eslint-enable security/detect-object-injection */
}

/**
 * Returns a translator bound to a single locale — sugar for routes
 * that resolve the locale once at the top and want to call `t(key)` /
 * `tf(key, params)` without threading the locale through every site.
 */
export function createTranslator(locale: Locale): {
  t: (key: I18nKey) => string;
  tf: (key: I18nKey, params: Readonly<Record<string, string | number>>) => string;
} {
  return {
    t: (key) => t(key, locale),
    tf: (key, params) => tf(key, params, locale),
  };
}

/**
 * Prefixes a path with the locale segment for non-default locales.
 * `/foo` stays `/foo` for ka; becomes `/en/foo` for en. Used wherever
 * we generate internal hrefs so the language-toggle round-trip works.
 */
export function localizedHref(path: string, locale: Locale): string {
  if (locale === DEFAULT_LOCALE) return path;
  const normalized = path === '/' ? '' : path.startsWith('/') ? path : `/${path}`;
  return `/${locale}${normalized}`;
}

const VALID_LOCALES: ReadonlySet<string> = new Set<Locale>(['ka', 'en']);

/**
 * Narrows a runtime string (e.g. `params.locale` from a dynamic
 * segment) to a `Locale`. Returns null for unknown values — the
 * caller decides whether to 404 or fall back.
 */
export function localeFromString(value: unknown): Locale | null {
  if (typeof value !== 'string') return null;
  return VALID_LOCALES.has(value) ? (value as Locale) : null;
}

export { en, ka };
