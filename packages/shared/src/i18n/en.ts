// English (en) catalog for PlayMe. Sprint 1 covers the minimum keys needed
// for landing, configure, and room flows. Sprint 6 wires the full
// i18next + ka pipeline; for now both en and ka are flat key → string maps
// and the web defaults to en.

export const en = {
  // --- Site chrome ---
  // Keyword-first: the descriptive phrase leads so it survives SERP
  // truncation, and the (generic, contested) brand is demoted to the tail.
  'site.title': 'Play casual games online with a friend, no signup — PlayMe',
  // Shorter brand form used by surfaces with limited space (e.g. the PWA
  // manifest `name`, shown on install prompts and the home screen).
  'site.titleShort': 'PlayMe — Play casual games online with a friend',
  // Landing-page <h1>. The visible wordmark is aria-hidden, so this is the
  // heading's whole accessible name; it also puts a real keyword phrase in
  // the highest-authority page's H1 (the wordmark text stays crawlable).
  'site.homeH1': 'PlayMe — play casual games online with a friend',
  'site.titleSuffix': '— PlayMe',
  'site.tagline': 'Play casual games online with a friend, no signup.',
  // Homepage-specific meta/OG description. Distinct from `site.tagline`
  // (which stays the site-wide OG fallback and the per-room default) so
  // the landing page's search snippet reads on its own terms.
  'site.homeMetaDescription':
    'Play 2-player board and strategy games online with a friend — no signup. Pick a game and share a link. Free to play.',
  // Short brand tagline used as the OG image kicker and the landing
  // hero subtitle, where the descriptive tagline (above) is too long.
  // Brand kit voice: short, punchy, board-game flavored.
  'site.brandTagline': 'Your move.',
  'site.ogImageAlt': 'PlayMe — play casual games online with a friend, no signup.',
  'site.howItWorks.title': 'How PlayMe works',
  'site.howItWorks.step1.title': 'Pick a game',
  'site.howItWorks.step1.body':
    'Choose a game and configure your side. No account needed.',
  'site.howItWorks.step2.title': 'Share the link',
  'site.howItWorks.step2.body':
    'You get a private room link. Send it to whoever you want to play with.',
  'site.howItWorks.step3.title': 'Play live',
  'site.howItWorks.step3.body':
    'The first friend who opens your link joins. Take turns in real time.',
  'site.catalog.title': 'Choose a game',

  // --- Footer ---
  // `{year}` is interpolated at render with the current year so the
  // notice never goes stale (see app/[locale]/copyright/page.tsx and the
  // footer in app/layout.tsx).
  'site.footer.copyright': '© {year} PlayMe.ge',

  // --- Copyright page ---
  'copyright.title': 'Copyright',
  'copyright.metaDescription':
    'Copyright and ownership information for PlayMe, a free platform for playing casual games online with a friend.',
  'copyright.notice': '© {year} PlayMe.ge. All rights reserved.',
  'copyright.body':
    'PlayMe.ge is a free, anonymous platform for playing casual two-player games online with a friend — no signup, just share a link. The PlayMe.ge name, logo, site design, and source code are protected by copyright. The classic games offered here (Tic-Tac-Toe, Connect 4, Reversi, Sea Battle) are in the public domain; only this site’s implementation and presentation are covered.',

  // --- About page ---
  // First-person: this is a personal learning project, so the voice is the
  // author's own. Each bullet is a bold lead-in (`*.label`) plus body text.
  'about.title': 'About',
  'about.metaDescription':
    'About PlayMe.ge — a personal learning project for playing casual two-player games online with a friend, built with AI pair-programming.',
  'about.intro':
    'PlayMe.ge is a personal project: a platform for playing online with a friend — no signup, just share a link and play a variety of board and strategy games.',
  'about.why.label': 'What I built it for',
  'about.why.body':
    'To explore the capabilities of AI — and for the fun of it. Not with abstract examples, but by building a real product.',
  'about.how.label': 'How I built it',
  'about.how.body': 'With the help of AI, from start to finish. I used Claude Code.',
  'about.stack.label': 'Under the hood',
  'about.stack.body':
    'A Next.js web app on the frontend, an ASP.NET Core API on the backend, real-time play over SignalR, and match state in Redis.',

  // --- Game names + rules ---
  // `metaTitle` / `metaDescription` are SEO-only (document <title> +
  // meta/OG description). They deliberately carry search synonyms the
  // clean on-screen labels (`name` / `shortDescription`) don't, so the
  // visible UI stays uncluttered while search queries still match.
  'games.tictactoe.name': 'Tic-Tac-Toe',
  'games.tictactoe.metaTitle': 'Tic-Tac-Toe online — play with a friend | PlayMe',
  'games.tictactoe.metaDescription':
    'Play Tic-Tac-Toe (noughts and crosses) online with a friend — 3×3, 6×6, or 9×9 board. No signup, just share a link.',
  'games.tictactoe.shortDescription':
    'Pick a board size: 3×3, 6×6, or 9×9. First to align the target number of marks wins.',
  'games.tictactoe.rules':
    'Players alternate placing X and O on a grid. On 3×3, align three marks to win; on 6×6, at least four; on 9×9, at least five. Lines count horizontally, vertically, or diagonally. Runs longer than the target on the bigger boards still count as a single win. If the board fills with no line, the game is a draw. X always moves first.',
  'games.tictactoe.sideX': 'X (moves first)',
  'games.tictactoe.sideO': 'O',
  'games.tictactoe.shortSideX': 'X',
  'games.tictactoe.shortSideO': 'O',

  'games.connect4.name': 'Connect 4',
  'games.connect4.metaTitle': 'Connect 4 online — play with a friend | PlayMe',
  'games.connect4.metaDescription':
    'Play Connect 4 (Four in a Row) online with a friend — drop discs and line up four first. No signup, just share a link.',
  'games.connect4.shortDescription':
    'Drop discs into a 7×6 grid with gravity. First to line up four wins.',
  'games.connect4.rules':
    'Players alternate dropping red and yellow discs into a 7-column, 6-row board. Each disc falls to the lowest empty cell in the chosen column. The first to align four of their discs — horizontally, vertically, or diagonally — wins. If the board fills with no line, the game is a draw. Red always moves first.',
  'games.connect4.sideRed': 'Red (moves first)',
  'games.connect4.sideYellow': 'Yellow',
  'games.connect4.shortSideRed': 'Red',
  'games.connect4.shortSideYellow': 'Yellow',

  'games.reversi.name': 'Reversi',
  'games.reversi.metaTitle': 'Reversi (Othello) online — play with a friend | PlayMe',
  'games.reversi.metaDescription':
    "Play Reversi (Othello) online with a friend on an 8×8 board — flip your opponent's discs. No signup, just share a link.",
  'games.reversi.shortDescription':
    "Sandwich your opponent's discs between yours to flip them. Whoever has more discs at the end wins.",
  'games.reversi.rules':
    "Two players alternate placing discs on an 8×8 board: dark first, then light. The first four placements must go in the central 2×2 squares and flip nothing — this is the classic free opening. From move 5 onward, every placement must bracket at least one of the opponent's discs between the new disc and another of your own in a straight line (horizontal, vertical, or diagonal); all bracketed opponent discs in every direction flip to your colour in a single move. If you have no legal move, the game passes for you automatically. The match ends when the board is full or both sides pass in succession. The player with more discs wins; equal counts is a draw.",
  'games.reversi.sideDark': 'Dark (moves first)',
  'games.reversi.sideLight': 'Light',
  'games.reversi.shortSideDark': 'Dark',
  'games.reversi.shortSideLight': 'Light',

  // --- Configure page ---
  'configure.title': 'Configure room',
  'configure.displayName.label': 'Your name',
  'configure.displayName.placeholder': 'How should we call you?',
  'configure.sideMode.label': 'How to assign sides',
  'configure.sideMode.hostPicksSpecific': "I'll pick my side",
  'configure.sideMode.random': 'Random',
  'configure.sideMode.challengerPicks': 'Let my friend pick',
  'configure.hostSide.label': 'Your side',
  'configure.boardSize.label': 'Board size',
  'configure.submit': 'Create room',
  'configure.submitting': 'Creating…',
  'configure.rules.title': 'How to play',
  'configure.back': 'Back to lobby',

  // --- Room / join ---
  'join.title': 'Join the room',
  'join.displayName.label': 'Your name',
  'join.displayName.placeholder': 'How should we call you?',
  'join.side.label': 'Pick your side',
  'join.submit': 'Join',
  'join.submitting': 'Joining…',
  'join.waiting': 'Waiting for your friend to open the link…',
  'join.waitingForStart': 'Waiting for the match to start…',
  'join.waitingForHost': 'Waiting for the host to reconnect…',
  'join.shareLink.label': 'Share this link with your friend',
  'join.shareLink.copy': 'Copy link',
  'join.shareLink.copied': 'Copied!',
  'join.shareLink.share': 'Share…',
  'join.shareLink.shareTitle': 'PlayMe — game invite',
  'join.shareLink.shareText.1': 'Join me for a quick game on PlayMe.',
  'join.shareLink.shareText.2': 'Join me on PlayMe.',
  'join.shareLink.shareText.3': "I'm waiting for you on PlayMe.",
  'join.shareLink.shareText.4': "Let's play on PlayMe.",
  'join.invite.headline': 'You’ve been invited to play',
  'join.invite.host': 'Host',
  'join.invite.game': 'Game',
  'join.invite.rules': 'Rules',

  // --- Match UI ---
  'match.yourTurn': 'Your turn',
  'match.opponentTurn': "Opponent's turn",
  'match.you': 'You',
  'match.opponent': 'Opponent',
  'match.opponentDisconnected': 'Opponent disconnected.',
  'match.opponentReconnected': 'Opponent reconnected.',
  'match.reconnecting': 'Reconnecting…',
  'match.connectionLost': 'Connection lost.',
  'match.reconnect': 'Reconnect',
  'match.clock.label': 'Clock',
  'match.result.youWin': 'You win!',
  'match.result.youLose': 'You lose.',
  'match.result.draw': 'Draw.',
  'match.result.youTimedOut': 'You ran out of time.',
  'match.result.opponentTimedOut': 'Opponent ran out of time.',
  'match.result.youResigned': 'You resigned.',
  'match.result.opponentResigned': 'Opponent resigned.',
  'match.result.youDisconnected': 'You were disconnected.',
  'match.result.opponentDisconnected': 'Opponent was disconnected.',
  'match.resign.button': 'Resign',
  'match.resign.confirm.title': 'Resign this match?',
  'match.resign.confirm.body': "You'll lose this match.",
  'match.resign.confirm.yes': 'Resign',
  'match.resign.confirm.cancel': 'Cancel',
  // Leaving during the setup phase (back arrow on the placement screen).
  // Client-side navigation only — the room stays alive until the setup
  // deadline, so the player can return via the invite link.
  'match.leaveSetup.confirm.title': 'Leave the room?',
  'match.leaveSetup.confirm.body': 'You can return through the shared invite link.',
  'match.leaveSetup.confirm.yes': 'Leave',
  'match.leaveSetup.confirm.cancel': 'Cancel',
  'match.backToLobby': 'Back to lobby',
  // Short form shown on narrow boards (@container match) where the full
  // label wraps; the full text stays the button's accessible name.
  'match.backToLobby.short': 'Back',
  'match.opponentLeft': 'Opponent left.',
  'room.expired.title': 'Room expired',
  'room.expired.body.unjoined':
    'No one joined in 30 minutes, so this room is gone. Start a new one when you’re ready.',
  'room.expired.body.setupTimeout':
    'Neither player finished setting up in time, so this room is closed. Start a new one when you’re ready.',
  'room.expired.cta': 'Back to home',
  'match.rematch.offer.button': 'Offer rematch',
  // Short form for narrow boards; full label stays the accessible name.
  'match.rematch.offer.short': 'Rematch',
  'match.rematch.accept.button': 'Accept rematch',
  'match.rematch.reject.button': 'Reject rematch',
  'match.rematch.waiting': 'Waiting for opponent…',
  'match.rematch.offered': 'Your opponent wants a rematch.',
  'match.rematch.declined': 'Opponent declined rematch.',
  'match.rematch.confirmReject.title': 'Reject this rematch?',
  'match.rematch.confirmReject.body': "You'll return to the lobby.",
  'match.rematch.confirmReject.yes': 'Reject',
  'match.rematch.confirmReject.cancel': 'Cancel',
  'match.score.label': 'Series',
  'match.score.draws.one': '1 draw',
  'match.score.draws.other': '{count} draws',
  'match.score.wins.one': '1 win',
  'match.score.wins.other': '{count} wins',
  'match.rules.button': 'Rules',
  'match.rules.close': 'Close',
  'match.board.label': 'Game board',
  'match.board.cell.label': 'Row {row} column {col}',

  // --- Connect 4 a11y labels (per-module vocab) ---
  'games.connect4.board.label': 'Connect 4 board',
  'games.connect4.columns.top': 'Connect 4 columns (top)',
  'games.connect4.columns.bottom': 'Connect 4 columns (bottom)',
  'games.connect4.dropColumn': 'Drop disc in column {col}',
  'games.connect4.cell.discRed': 'Red disc',
  'games.connect4.cell.discYellow': 'Yellow disc',
  'games.connect4.cell.empty': 'Empty cell row {row}',

  // --- Reversi a11y labels (per-module vocab) ---
  'games.reversi.board.label': 'Reversi board',
  'games.reversi.cell.discDark': 'Dark disc',
  'games.reversi.cell.discLight': 'Light disc',
  'games.reversi.cell.empty': 'Empty cell row {row} column {col}',
  'games.reversi.cell.legal': 'Legal move row {row} column {col}',
  'games.reversi.score.dark': '{count} dark',
  'games.reversi.score.light': '{count} light',
  'games.reversi.toast.autoPassSelf': 'No legal moves — opponent plays again.',
  'games.reversi.toast.autoPassOpponent': 'Opponent has no moves — you play again.',
  'games.seabattle.name': 'Sea Battle',
  'games.seabattle.metaTitle': 'Sea Battle (Battleship) online — play with a friend | PlayMe',
  'games.seabattle.metaDescription':
    'Play Sea Battle (Battleship) online with a friend — secretly place your fleet, call your shots, sink every enemy ship. No signup, just share a link.',
  'games.seabattle.shortDescription':
    'Hide a fleet of ten ships, then trade shots. A hit earns another shot. The first to sink the opponent’s entire fleet wins.',
  'games.seabattle.rules':
    'Each player secretly places ten ships of different sizes on their own 10×10 grid: one four-decker, two three-deckers, three two-deckers, and four single-deckers. Ships are straight lines and may not touch each other (not even diagonally). Players then take turns calling shots at the opponent’s grid; each shot is answered miss, hit, or sunk. A hit or a sunk ship earns you another shot — you keep firing until you miss. Shooting the same cell twice is not allowed. The first player to sink the entire enemy fleet wins. A draw is impossible.',
  'games.seabattle.sideFirst': 'I’ll go first',
  'games.seabattle.sideSecond': 'Opponent goes first',
  'games.seabattle.shortSideFirst': 'First',
  'games.seabattle.shortSideSecond': 'Second',
  'games.seabattle.setup.title': 'Place your fleet',
  'games.seabattle.setup.hint':
    'Your ships are placed randomly — shuffle until you like the layout, then confirm. Your opponent never sees your fleet.',
  'games.seabattle.setup.reroll': 'Shuffle ships',
  'games.seabattle.setup.commit': 'Confirm fleet',
  'games.seabattle.setup.committed': 'Fleet confirmed — waiting for your opponent…',
  'games.seabattle.setup.opponentReady': 'Your opponent is ready.',
  'games.seabattle.setup.opponentPlacing': 'Your opponent is placing their fleet.',
  'games.seabattle.board.yours': 'Your fleet',
  'games.seabattle.board.target': 'Enemy waters',
  // Short forms for the narrow-screen board pager pills. English already
  // fits on one line, so they match the board headings; Georgian shortens.
  'games.seabattle.tab.yours': 'Your fleet',
  'games.seabattle.tab.target': 'Enemy waters',
  'games.seabattle.cell.fire': 'Fire at row {row}, column {col}',
  'games.seabattle.cell.miss': 'Miss at row {row}, column {col}',
  'games.seabattle.cell.hit': 'Hit at row {row}, column {col}',
  'games.seabattle.cell.sunk': 'Sunk ship cell at row {row}, column {col}',
  'games.seabattle.cell.ship': 'Your ship at row {row}, column {col}',
  'games.seabattle.cell.water': 'Water at row {row}, column {col}',
  'games.seabattle.feedback.hit': 'Hit — shoot again!',
  'games.seabattle.feedback.sunk': 'Ship sunk — shoot again!',
  'games.seabattle.feedback.miss': 'Miss.',
  'games.seabattle.setup.committing': 'Confirming…',

  // --- PWA install prompt (Sprint 6) ---
  'pwa.install.title': 'Install PlayMe as an app',
  'pwa.install.cta': 'Install',
  'pwa.install.dismiss': 'Dismiss',

  // --- Locale toggle (Sprint 6) ---
  // The visible label is the static 2-letter code; this string feeds
  // the aria-label, which describes the *next* state (the click's
  // effect) — same pattern as the theme toggle.
  'locale.switch.toKa': 'Switch to Georgian',
  'locale.switch.toEn': 'Switch to English',

  // --- Theme toggle (Sprint 6) ---
  // The aria-label describes the *next* state because the button cycles
  // light → dark → system → light. Sighted users see the current state
  // via the icon (sun / moon / monitor); the label is for screen readers.
  'theme.toggle.next.dark': 'Switch to dark theme',
  'theme.toggle.next.system': 'Use system theme',
  'theme.toggle.next.light': 'Switch to light theme',

  // --- App-level error / not-found chrome ---
  'errors.boundary.title': 'Something went wrong.',
  'errors.boundary.retry': 'Try again',
  'notFound.home': '← Home',

  // --- Errors (mirror ErrorCode i18n keys from CLAUDE.md §3) ---
  'errors.unknown': 'Something went wrong. Please try again.',
  'errors.validation.displayName': 'That name isn’t valid.',
  'errors.validation.move': 'That move isn’t valid.',
  'errors.config.invalidGameId': 'Unknown game.',
  'errors.config.invalidSideSelectionMode': 'Pick a way to assign sides.',
  'errors.config.invalidHostSide': "That side can't be picked here.",
  'errors.config.invalidGameOptions': "Those game settings aren't valid.",
  'errors.join.sideNotAllowed': "The host already chose sides — you can't pick.",
  'errors.join.sidePickRequired': 'Pick a side to continue.',
  'errors.join.invalidSide': "That side doesn't exist for this game.",
  'errors.room.notFound': 'This room link is dead or has expired.',
  'errors.room.alreadyJoined': 'Someone already joined this room.',
  'errors.room.notJoinable': 'This room is no longer accepting players.',
  'errors.room.busy': 'Server is busy. Try again in a moment.',
  'errors.move.illegalCell': "You can't play there.",
  'errors.move.cellOccupied': 'That cell is already taken.',
  'errors.move.illegalColumn': "That column doesn't exist.",
  'errors.move.columnFull': 'That column is full.',
  'errors.move.outOfBounds': 'That spot is off the board.',
  'errors.move.mustBracket':
    "You can only place where you'd flip at least one of the opponent's discs.",
  'errors.move.openingMustBeCentral': 'Opening moves go in the central four squares.',
  'errors.move.alreadyShot': "You've already fired at that cell.",
  'errors.move.notYourTurn': "It's not your turn.",
  'errors.move.matchNotInProgress': 'The match has already ended.',
  'errors.session.unauthorized': 'Your session is invalid. Open the room link again.',
  'errors.rate.exceeded': 'You’re going too fast. Wait a moment and try again.',
  'errors.exit.notAllowed': "Can't exit right now.",
  'errors.rematch.invalidState': "Can't offer a rematch right now.",
  'errors.rematch.notResponder': 'Only your opponent can accept or reject.',
  'errors.setup.notInSetup': 'The setup phase is over.',
  'errors.setup.alreadyCommitted': "You've already confirmed your setup.",
  'errors.setup.invalidFleet': 'That fleet placement is invalid. Shuffle and try again.',
} as const;

export type EnKey = keyof typeof en;
