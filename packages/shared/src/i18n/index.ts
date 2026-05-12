import { en, type EnKey } from './en';
import { ka } from './ka';

export type Locale = 'en' | 'ka';
export type I18nKey = EnKey;

const catalogs: Record<Locale, Record<EnKey, string>> = {
  en,
  ka,
};

export const DEFAULT_LOCALE: Locale = 'en';

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

export { en, ka };
