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
  /**
   * Decorative mid-game position for the landing-page card. Flat row-major
   * cells of length rows*cols, each entry a side id from `sides` or null.
   * Purely cosmetic — not parsed by any game module and not bound to
   * server state.
   */
  readonly preview: readonly (string | null)[];
}

export const GAME_CATALOG: readonly GameCatalogEntry[] = [
  // Sprint 9 PR2: one unified Tic-Tac-Toe entry. Board size becomes a
  // per-room option the host picks on the configure page (gameOptions.
  // boardSize ∈ {3,6,9}); rows/cols here size the decorative landing-page
  // preview tile only — the runtime board sizes from the server-side
  // state. Legacy `tictactoe-3x3` / `-6x6` / `-9x9` slugs are 301-redirected
  // here via next.config.js redirects().
  {
    id: 'tictactoe',
    slug: 'tictactoe',
    nameKey: 'games.tictactoe.name',
    shortDescriptionKey: 'games.tictactoe.shortDescription',
    rulesKey: 'games.tictactoe.rules',
    sides: [
      { id: 'x', labelKey: 'games.tictactoe.sideX' },
      { id: 'o', labelKey: 'games.tictactoe.sideO' },
    ],
    defaultHostSide: 'x',
    rows: 3,
    cols: 3,
    // X _ O
    // _ X _
    // _ _ O
    preview: ['x', null, 'o', null, 'x', null, null, null, 'o'],
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
    // Gravity-valid mid-game position.
    // . . . . . . .
    // . . . . . . .
    // . . . R . . .
    // . . R Y R . .
    // . Y R Y Y . .
    // R Y R R Y Y .
    preview: [
      null, null, null, null,    null,    null,    null,
      null, null, null, null,    null,    null,    null,
      null, null, null, 'red',   null,    null,    null,
      null, null, 'red', 'yellow', 'red', null,    null,
      null, 'yellow', 'red', 'yellow', 'yellow', null, null,
      'red', 'yellow', 'red', 'red', 'yellow', 'yellow', null,
    ],
  },
  {
    id: 'reversi',
    slug: 'reversi',
    nameKey: 'games.reversi.name',
    shortDescriptionKey: 'games.reversi.shortDescription',
    rulesKey: 'games.reversi.rules',
    sides: [
      { id: 'dark', labelKey: 'games.reversi.sideDark' },
      { id: 'light', labelKey: 'games.reversi.sideLight' },
    ],
    defaultHostSide: 'dark',
    rows: 8,
    cols: 8,
    // Mid-game spread that preserves the standard Reversi opening in the
    // centre 2×2 (L D / D L at (3,3)..(4,4)) — the initial position dictated
    // by the rules — with a few additional moves rippling outward.
    // . . . . . . . .
    // . . . . . . . .
    // . . . D D . . .
    // . . . L D D . .
    // . . D D L . . .
    // . . . L D . . .
    // . . . . . . . .
    // . . . . . . . .
    preview: [
      null, null, null, null,    null,    null,   null, null,
      null, null, null, null,    null,    null,   null, null,
      null, null, null, 'dark',  'dark',  null,   null, null,
      null, null, null, 'light', 'dark',  'dark', null, null,
      null, null, 'dark', 'dark', 'light', null,  null, null,
      null, null, null, 'light', 'dark',  null,   null, null,
      null, null, null, null,    null,    null,   null, null,
      null, null, null, null,    null,    null,   null, null,
    ],
  },
];

export function findGame(slugOrId: string): GameCatalogEntry | undefined {
  return GAME_CATALOG.find((g) => g.slug === slugOrId || g.id === slugOrId);
}
