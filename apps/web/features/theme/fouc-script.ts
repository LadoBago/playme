// Synchronous inline script injected into <head> by the root layout.
// Runs before the first paint so the chosen theme is applied without
// a light/dark flash. Mirrors readStoredTheme + applyTheme from
// ./theme-storage, but inlined as a string because it executes
// before any JS modules load. The CSP nonce is attached by the
// layout when this is rendered.

import { DEFAULT_THEME, STORAGE_KEY } from './theme-storage';

const QUOTED_KEY = JSON.stringify(STORAGE_KEY);
const QUOTED_DEFAULT = JSON.stringify(DEFAULT_THEME);

export const themeFoucScript = `(function(){try{var v=localStorage.getItem(${QUOTED_KEY});if(v!=="light"&&v!=="dark"&&v!=="system")v=${QUOTED_DEFAULT};document.documentElement.setAttribute("data-theme",v);}catch(e){document.documentElement.setAttribute("data-theme",${QUOTED_DEFAULT});}})();`;
