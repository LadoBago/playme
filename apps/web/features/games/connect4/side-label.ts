import { t, type Locale } from '@playme/shared';

/**
 * Localised short side label ("Red" / "Yellow") for the platform's player
 * card. Side vocab stays inside this module (CLAUDE.md §7 "Platform
 * thinness"); the platform calls through `GameModule.getSideLabel`.
 *
 * Lives apart from the view so the registry can import it eagerly (the
 * match header resolves side labels synchronously) while the renderer
 * itself stays behind `next/dynamic`.
 */
export function connect4SideLabel(side: string, locale: Locale): string | null {
  if (side === 'red') return t('games.connect4.shortSideRed', locale);
  if (side === 'yellow') return t('games.connect4.shortSideYellow', locale);
  return null;
}
