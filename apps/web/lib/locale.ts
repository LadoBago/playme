import { headers } from 'next/headers';
import { notFound } from 'next/navigation';
import {
  createTranslator,
  DEFAULT_LOCALE,
  localeFromString,
  type Locale,
} from '@playme/shared';

// Server-side helpers for locale-aware routes. Use `resolveLocale` in
// async page/layout functions to validate the [locale] segment param;
// use `getServerLocale` / `getServerTranslator` where params aren't
// available (root layout, not-found, manifest, opengraph-image), which
// fall back to the `x-locale` request header set by proxy.ts (the
// Next.js middleware).

export async function resolveLocale(
  paramsPromise: Promise<{ locale: string }>,
): Promise<Locale> {
  const { locale } = await paramsPromise;
  const resolved = localeFromString(locale);
  if (!resolved) notFound();
  return resolved;
}

export async function getServerLocale(): Promise<Locale> {
  const value = (await headers()).get('x-locale');
  return localeFromString(value) ?? DEFAULT_LOCALE;
}

export async function getServerTranslator(): Promise<
  ReturnType<typeof createTranslator> & { locale: Locale }
> {
  const locale = await getServerLocale();
  return { ...createTranslator(locale), locale };
}
