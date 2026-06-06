'use client';

import { usePathname, useRouter } from 'next/navigation';
import { type I18nKey, type Locale, localizedHref } from '@playme/shared';
import { useTranslator } from '@/lib/use-locale';

// Circular toggle that mirrors the theme toggle's shape. Shows the
// active locale's 2-letter code; clicking switches to the other.
// Source of truth is the URL — no localStorage, no cookie — so a
// shared link in the default locale still opens in that locale for
// the recipient.

function nextLocale(current: Locale): Locale {
  return current === 'ka' ? 'en' : 'ka';
}

function labelFor(locale: Locale): string {
  switch (locale) {
    case 'ka':
      return 'KA';
    case 'en':
      return 'EN';
  }
}

function ariaKeyToNext(next: Locale): I18nKey {
  switch (next) {
    case 'ka':
      return 'locale.switch.toKa';
    case 'en':
      return 'locale.switch.toEn';
  }
}

function bareFromPath(pathname: string): string {
  if (pathname === '/en') return '/';
  if (pathname.startsWith('/en/')) return pathname.slice(3);
  return pathname;
}

export function LocaleToggle() {
  const router = useRouter();
  const pathname = usePathname();
  const { t, locale } = useTranslator();
  const next = nextLocale(locale);

  // WCAG 2.5.3 (Label in Name): the visible text ("KA"/"EN") must be part
  // of the accessible name so voice-control users can activate the button
  // by saying what they see.
  return (
    <button
      type="button"
      className="locale-toggle"
      onClick={() => router.push(localizedHref(bareFromPath(pathname), next))}
      aria-label={`${labelFor(locale)} — ${t(ariaKeyToNext(next))}`}
    >
      {labelFor(locale)}
    </button>
  );
}
