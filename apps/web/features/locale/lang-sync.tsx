'use client';

import { useEffect } from 'react';
import { useLocale } from '@/lib/use-locale';

// The root layout owns <html lang> and reads the locale from the
// middleware-supplied x-locale request header, which is correct on
// first paint. But Next's client router preserves the root layout
// across route changes, so when the user navigates ka ↔ en the
// <html> element doesn't re-render. This effect closes the gap by
// updating document.documentElement.lang on every locale change.
// useLocale() also drives useTranslator(), so the attribute always
// matches the strings the user sees.
export function LangSync() {
  const locale = useLocale();
  useEffect(() => {
    if (typeof document === 'undefined') return;
    document.documentElement.lang = locale;
  }, [locale]);
  return null;
}
