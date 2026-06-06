import { t as translate, type Locale } from '@playme/shared';

/**
 * Localised short side label ("First" / "Second") for the platform's
 * player card. Side vocab stays inside this module (CLAUDE.md §7
 * "Platform thinness").
 *
 * Lives apart from the view so the registry can import it eagerly (the
 * match header resolves side labels synchronously) while the renderer
 * itself stays behind `next/dynamic`.
 */
export function seabattleSideLabel(side: string, locale: Locale): string | null {
  if (side === 'first') return translate('games.seabattle.shortSideFirst', locale);
  if (side === 'second') return translate('games.seabattle.shortSideSecond', locale);
  return null;
}
