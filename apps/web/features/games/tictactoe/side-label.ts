import { t, type Locale } from '@playme/shared';

/**
 * Localised short side label ("X" / "O") for the platform's player card.
 * Side vocab stays inside this module (CLAUDE.md §7 "Platform thinness");
 * the platform calls through `GameModule.getSideLabel`. Shared with the
 * legacy per-size renderers via the same `games.tictactoe.shortSide*` keys.
 *
 * Lives apart from the view so the registry can import it eagerly (the
 * match header resolves side labels synchronously) while the renderer
 * itself stays behind `next/dynamic`.
 */
export function tictactoeSideLabel(side: string, locale: Locale): string | null {
  if (side === 'x') return t('games.tictactoe.shortSideX', locale);
  if (side === 'o') return t('games.tictactoe.shortSideO', locale);
  return null;
}
