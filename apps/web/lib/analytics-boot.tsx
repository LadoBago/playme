'use client';

import { useEffect } from 'react';
import { initAnalytics } from './analytics';

/**
 * Mounted once from RootLayout. Fires PostHog init on the first render
 * in the browser — server-render is a no-op (initAnalytics guards on
 * `window`).
 */
export function AnalyticsBoot(): null {
  useEffect(() => {
    initAnalytics();
  }, []);
  return null;
}
