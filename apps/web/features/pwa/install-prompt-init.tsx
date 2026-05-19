'use client';

import { useEffect } from 'react';
import { initInstallStore } from './install-store';

// Mount once from the root layout. Attaches the `beforeinstallprompt`
// + `appinstalled` listeners on the first client render; idempotent if
// React re-mounts. Returns null — the visible install affordance is
// `<InstallPrompt />`, rendered on the landing page only.
export function InstallPromptInit() {
  useEffect(() => {
    initInstallStore();
  }, []);
  return null;
}
