'use client';

import { usePathname, useRouter } from 'next/navigation';
import { type I18nKey, type Locale, localizedHref } from '@playme/shared';
import { useTranslator } from '@/lib/use-locale';

// Slim pill switcher (KA | EN) that sits in the root-layout toolbar
// next to the theme toggle. Source of truth is the URL — clicking
// the inactive locale navigates to the same path under the other
// locale's prefix; we don't persist the choice in localStorage on
// purpose, so a shared link in the default locale stays in that
// locale for the recipient.

const LOCALES: readonly Locale[] = ['ka', 'en'];

function labelFor(locale: Locale): string {
  switch (locale) {
    case 'ka':
      return 'KA';
    case 'en':
      return 'EN';
  }
}

function ariaKeyFor(locale: Locale): I18nKey {
  switch (locale) {
    case 'ka':
      return 'locale.name.ka';
    case 'en':
      return 'locale.name.en';
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
  const { t, locale: current } = useTranslator();
  const bare = bareFromPath(pathname);

  function navigate(target: Locale) {
    if (target === current) return;
    router.push(localizedHref(bare, target));
  }

  return (
    <div className="locale-toggle" role="group" aria-label={t('locale.toggle.label')}>
      {LOCALES.map((loc) => {
        const active = loc === current;
        return (
          <button
            key={loc}
            type="button"
            className={`locale-toggle__button${active ? ' locale-toggle__button--active' : ''}`}
            onClick={() => navigate(loc)}
            aria-pressed={active}
            aria-label={t(ariaKeyFor(loc))}
          >
            {labelFor(loc)}
          </button>
        );
      })}
    </div>
  );
}
