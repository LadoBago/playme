import { useEffect, useState } from 'react';
import {
  applyTheme,
  readStoredTheme,
  writeStoredTheme,
  type Theme,
} from './theme-storage';

export type UseThemeResult = {
  // `null` until the component mounts on the client. The FOUC script
  // in app/layout.tsx has already set <html data-theme> before paint,
  // so a `null` initial value lets the toggle render nothing during
  // SSR + first render and avoid a hydration mismatch on the icon and
  // aria-label (the chosen theme isn't knowable until localStorage is
  // available).
  theme: Theme | null;
  setTheme: (theme: Theme) => void;
};

export function useTheme(): UseThemeResult {
  const [theme, setThemeState] = useState<Theme | null>(null);

  useEffect(() => {
    setThemeState(readStoredTheme());
  }, []);

  function setTheme(next: Theme): void {
    setThemeState(next);
    writeStoredTheme(next);
    applyTheme(next);
  }

  return { theme, setTheme };
}
