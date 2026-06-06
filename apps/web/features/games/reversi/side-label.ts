import { t, type Locale } from "@playme/shared";

/**
 * Localised short side label ("Dark" / "Light") for the platform's player
 * card. Side vocab stays inside this module (CLAUDE.md §7 "Platform
 * thinness"); the platform calls through `GameModule.getSideLabel`.
 *
 * Lives apart from the view so the registry can import it eagerly (the
 * match header resolves side labels synchronously) while the renderer
 * itself stays behind `next/dynamic`.
 */
export function reversiSideLabel(side: string, locale: Locale): string | null {
  if (side === "dark") return t("games.reversi.shortSideDark", locale);
  if (side === "light") return t("games.reversi.shortSideLight", locale);
  return null;
}
