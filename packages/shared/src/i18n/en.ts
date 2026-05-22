// English (en) catalog for PlayMe. Sprint 1 covers the minimum keys needed
// for landing, configure, and room flows. Sprint 6 wires the full
// i18next + ka pipeline; for now both en and ka are flat key → string maps
// and the web defaults to en.

export const en = {
  // --- Site chrome ---
  'site.title': 'PlayMe — Play casual games with a friend, no signup',
  // Shorter brand form used by surfaces with limited space (e.g. the PWA
  // manifest `name`, shown on install prompts and the home screen).
  'site.titleShort': 'PlayMe — Play casual games with a friend',
  'site.titleSuffix': '— PlayMe',
  'site.tagline': 'Play casual games with a friend, no signup.',
  // Short brand tagline used as the OG image kicker and the landing
  // hero subtitle, where the descriptive tagline (above) is too long.
  // Brand kit voice: short, punchy, board-game flavored.
  'site.brandTagline': 'Your move.',
  'site.ogImageAlt': 'PlayMe — play casual games with a friend, no signup.',
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

  // --- Game names + rules ---
  'games.tictactoe-3x3.name': 'Tic-Tac-Toe 3×3',
  'games.tictactoe-3x3.shortDescription':
    'The classic. First to align three Xs or Os in a row, column, or diagonal wins.',
  'games.tictactoe-3x3.rules':
    'Players alternate placing X and O on a 3×3 grid. The first to align three of their marks horizontally, vertically, or diagonally wins. If the board fills with no line, the game is a draw. X always moves first.',
  'games.tictactoe-6x6.name': 'Tic-Tac-Toe 6×6',
  'games.tictactoe-6x6.shortDescription':
    'Bigger board, longer lines. First to align four Xs or Os wins.',
  'games.tictactoe-6x6.rules':
    'Players alternate placing X and O on a 6×6 grid. The first to align at least four of their marks horizontally, vertically, or diagonally wins — a run of five or six in a row also wins. If the board fills with no line, the game is a draw. X always moves first.',
  'games.tictactoe-9x9.name': 'Tic-Tac-Toe 9×9',
  'games.tictactoe-9x9.shortDescription':
    'Five-in-a-row on a 9×9 grid. Place Xs and Os; first to align at least five wins.',
  'games.tictactoe-9x9.rules':
    'Players alternate placing X and O on a 9×9 grid. The first to align at least five of their marks consecutively — horizontally, vertically, or diagonally — wins. Longer runs (six, seven, eight, or nine in a row) also count as a single win. If the board fills with no line, the game is a draw. X always moves first.',
  'games.tictactoe.sideX': 'X (moves first)',
  'games.tictactoe.sideO': 'O',
  'games.tictactoe.shortSideX': 'X',
  'games.tictactoe.shortSideO': 'O',

  'games.connect4.name': 'Connect 4',
  'games.connect4.shortDescription':
    'Drop discs into a 7×6 grid with gravity. First to line up four wins.',
  'games.connect4.rules':
    'Players alternate dropping red and yellow discs into a 7-column, 6-row board. Each disc falls to the lowest empty cell in the chosen column. The first to align four of their discs — horizontally, vertically, or diagonally — wins. If the board fills with no line, the game is a draw. Red always moves first.',
  'games.connect4.sideRed': 'Red (moves first)',
  'games.connect4.sideYellow': 'Yellow',
  'games.connect4.shortSideRed': 'Red',
  'games.connect4.shortSideYellow': 'Yellow',

  'games.reversi.name': 'Reversi',
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
  'match.backToLobby': 'Back to lobby',
  'match.opponentLeft': 'Opponent left.',
  'room.expired.title': 'Room expired',
  'room.expired.body': 'No one joined in 30 minutes, so this room is gone. Start a new one when you’re ready.',
  'room.expired.cta': 'Back to home',
  'match.rematch.offer.button': 'Offer rematch',
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
  'games.reversi.toast.autoPass': 'No legal moves — passed automatically.',

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
  'errors.move.passNotAllowed': "You can't pass — you still have a legal move.",
  'errors.move.notYourTurn': "It's not your turn.",
  'errors.move.matchNotInProgress': 'The match has already ended.',
  'errors.session.unauthorized': 'Your session is invalid. Open the room link again.',
  'errors.rate.exceeded': 'You’re going too fast. Wait a moment and try again.',
  'errors.exit.notAllowed': "Can't exit right now.",
  'errors.rematch.invalidState': "Can't offer a rematch right now.",
  'errors.rematch.notResponder': 'Only your opponent can accept or reject.',
} as const;

export type EnKey = keyof typeof en;
