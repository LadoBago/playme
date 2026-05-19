'use client';

import { t } from '@playme/shared';
import { dismiss, install, useInstallStatus } from './install-store';

// Slim install banner. Renders only when the browser has fired
// `beforeinstallprompt`, the app isn't already installed, and the
// user hasn't dismissed the prompt. The Install button triggers the
// native install dialog; the × dismisses the banner (per-localStorage,
// so the choice persists across reloads).
export function InstallPrompt() {
  const { promptable } = useInstallStatus();
  if (!promptable) return null;

  return (
    <div className="install-prompt">
      <DownloadIcon />
      <span className="install-prompt__text">{t('pwa.install.title')}</span>
      <button
        type="button"
        className="button-primary install-prompt__cta"
        onClick={() => {
          void install();
        }}
      >
        {t('pwa.install.cta')}
      </button>
      <button
        type="button"
        className="install-prompt__dismiss"
        onClick={dismiss}
        aria-label={t('pwa.install.dismiss')}
      >
        <CloseIcon />
      </button>
    </div>
  );
}

function DownloadIcon() {
  return (
    <svg
      width="22"
      height="22"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      className="install-prompt__icon"
    >
      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
      <polyline points="7 10 12 15 17 10" />
      <line x1="12" y1="15" x2="12" y2="3" />
    </svg>
  );
}

function CloseIcon() {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      <line x1="18" y1="6" x2="6" y2="18" />
      <line x1="6" y1="6" x2="18" y2="18" />
    </svg>
  );
}
