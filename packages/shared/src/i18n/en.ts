// English (en) catalog for PlayMe. Sprint 1 covers the minimum keys needed
// for landing, configure, and room flows. Sprint 6 wires the full
// i18next + ka pipeline; for now both en and ka are flat key → string maps
// and the web defaults to en.

export const en = {
  // --- Site chrome ---
  'site.title': 'PlayMe — Play casual games with a friend, no signup',
  'site.titleSuffix': '— PlayMe',
  'site.tagline': 'Play casual games with a friend, no signup.',
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

  // --- Room / join ---
  'join.title': 'Join the room',
  'join.displayName.label': 'Your name',
  'join.displayName.placeholder': 'How should we call you?',
  'join.side.label': 'Pick your side',
  'join.submit': 'Join',
  'join.submitting': 'Joining…',
  'join.waiting': 'Waiting for your friend to open the link…',
  'join.shareLink.label': 'Share this link with your friend',
  'join.shareLink.copy': 'Copy link',
  'join.shareLink.copied': 'Copied!',
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
  'errors.move.notYourTurn': "It's not your turn.",
  'errors.move.matchNotInProgress': 'The match has already ended.',
  'errors.session.unauthorized': 'Your session is invalid. Open the room link again.',
  'errors.rate.exceeded': 'You’re going too fast. Wait a moment and try again.',
} as const;

export type EnKey = keyof typeof en;
