// Catalog of game modules. Slug equals the GameId from the API
// (CLAUDE.md §2.3, §2.5) — same identifier across URLs, room state,
// analytics, and the rules catalog.
//
// Sprint 1 ships with one entry; later sprints add the rest.

import type { I18nKey } from '../i18n/index';

export interface GameCatalogEntry {
  readonly id: string;
  readonly slug: string;
  readonly nameKey: I18nKey;
  readonly shortDescriptionKey: I18nKey;
  readonly rulesKey: I18nKey;
  readonly sides: readonly { readonly id: string; readonly labelKey: I18nKey }[];
  readonly defaultHostSide: string;
  /** Board dimensions for the configure-page preview / room renderer. */
  readonly rows: number;
  readonly cols: number;
}

export const GAME_CATALOG: readonly GameCatalogEntry[] = [
  {
    id: 'tictactoe-3x3',
    slug: 'tictactoe-3x3',
    nameKey: 'games.tictactoe-3x3.name',
    shortDescriptionKey: 'games.tictactoe-3x3.shortDescription',
    rulesKey: 'games.tictactoe-3x3.rules',
    sides: [
      { id: 'x', labelKey: 'games.tictactoe.sideX' },
      { id: 'o', labelKey: 'games.tictactoe.sideO' },
    ],
    defaultHostSide: 'x',
    rows: 3,
    cols: 3,
  },
  {
    id: 'tictactoe-6x6',
    slug: 'tictactoe-6x6',
    nameKey: 'games.tictactoe-6x6.name',
    shortDescriptionKey: 'games.tictactoe-6x6.shortDescription',
    rulesKey: 'games.tictactoe-6x6.rules',
    sides: [
      { id: 'x', labelKey: 'games.tictactoe.sideX' },
      { id: 'o', labelKey: 'games.tictactoe.sideO' },
    ],
    defaultHostSide: 'x',
    rows: 6,
    cols: 6,
  },
  {
    id: 'tictactoe-9x9',
    slug: 'tictactoe-9x9',
    nameKey: 'games.tictactoe-9x9.name',
    shortDescriptionKey: 'games.tictactoe-9x9.shortDescription',
    rulesKey: 'games.tictactoe-9x9.rules',
    sides: [
      { id: 'x', labelKey: 'games.tictactoe.sideX' },
      { id: 'o', labelKey: 'games.tictactoe.sideO' },
    ],
    defaultHostSide: 'x',
    rows: 9,
    cols: 9,
  },
  {
    id: 'connect4',
    slug: 'connect4',
    nameKey: 'games.connect4.name',
    shortDescriptionKey: 'games.connect4.shortDescription',
    rulesKey: 'games.connect4.rules',
    sides: [
      { id: 'red', labelKey: 'games.connect4.sideRed' },
      { id: 'yellow', labelKey: 'games.connect4.sideYellow' },
    ],
    defaultHostSide: 'red',
    rows: 6,
    cols: 7,
  },
];

export function findGame(slugOrId: string): GameCatalogEntry | undefined {
  return GAME_CATALOG.find((g) => g.slug === slugOrId || g.id === slugOrId);
}
