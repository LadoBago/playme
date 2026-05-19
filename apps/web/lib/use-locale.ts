'use client';

import { useParams } from 'next/navigation';
import {
  DEFAULT_LOCALE,
  createTranslator,
  localeFromString,
  type Locale,
} from '@playme/shared';

// Read the current locale from the active route's [locale] segment.
// Falls back to <html lang> for components mounted at root scope
// (notably app/error.tsx, which lives outside [locale] because error
// boundaries cannot receive params).
export function useLocale(): Locale {
  const params = useParams();
  const raw =
    params && typeof params === 'object' && 'locale' in params ? params.locale : null;
  const fromParams = localeFromString(raw);
  if (fromParams) return fromParams;
  if (typeof document !== 'undefined') {
    const fromHtml = localeFromString(document.documentElement.lang);
    if (fromHtml) return fromHtml;
  }
  return DEFAULT_LOCALE;
}

// Sugar for `const { t, tf } = createTranslator(useLocale())` —
// covers ~all client-component callers. Also exposes the locale
// itself so callers building localized hrefs don't need a second
// useLocale() call.
export function useTranslator(): ReturnType<typeof createTranslator> & {
  locale: Locale;
} {
  const locale = useLocale();
  return { ...createTranslator(locale), locale };
}
