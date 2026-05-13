// Georgian (ka) catalog. Sprint 6 wires the full ka/en switcher and
// completes the translations; Sprint 1 ships an initial pass mirroring the
// en keys 1:1 so the catalog files don't drift in shape.

import type { EnKey } from './en';

export const ka: Record<EnKey, string> = {
  // --- Site chrome ---
  'site.tagline': 'ითამაშე მეგობართან, რეგისტრაცია არ გჭირდება.',
  'site.howItWorks.title': 'როგორ მუშაობს PlayMe',
  'site.howItWorks.step1.title': 'აირჩიე თამაში',
  'site.howItWorks.step1.body':
    'აირჩიე თამაში და დააფიქსირე შენი მხარე. ანგარიში არ გჭირდება.',
  'site.howItWorks.step2.title': 'გააზიარე ბმული',
  'site.howItWorks.step2.body':
    'მიიღებ უნიკალურ ბმულს. გაუგზავნე იმას, ვისთანაც გინდა იყო.',
  'site.howItWorks.step3.title': 'ითამაშე ცოცხლად',
  'site.howItWorks.step3.body':
    'პირველი მეგობარი, რომელიც ბმულს გახსნის, შემოგიერთდება. ისვრიდე რიგრიგობით.',
  'site.catalog.title': 'აირჩიე თამაში',

  // --- Game names + rules ---
  'games.tictactoe-3x3.name': 'ჯვარედინა 3×3',
  'games.tictactoe-3x3.shortDescription':
    'კლასიკა. პირველი, ვინც 3-ს მწკრივში, სვეტში ან დიაგონალზე ერთად დააწყობს — იგებს.',
  'games.tictactoe-3x3.rules':
    'მოთამაშეები რიგრიგობით სვამენ X-ს ან O-ს 3×3 ბადეზე. ვინც პირველი ჩამოაწყობს სამ ერთსა და იმავე ნიშანს ჰორიზონტალურად, ვერტიკალურად ან დიაგონალზე — იგებს. თუ ბადე შეივსება გამარჯვებული მწკრივის გარეშე — ფრე. პირველი ხოდი ყოველთვის X-ს ეკუთვნის.',
  'games.tictactoe.sideX': 'X (პირველი ხოდი)',
  'games.tictactoe.sideO': 'O',

  // --- Configure page ---
  'configure.title': 'ოთახის კონფიგურაცია',
  'configure.displayName.label': 'შენი სახელი',
  'configure.displayName.placeholder': 'როგორ მოგმართოთ?',
  'configure.sideMode.label': 'მხარეების მინიჭება',
  'configure.sideMode.hostPicksSpecific': 'მე ავირჩევ ჩემს მხარეს',
  'configure.sideMode.random': 'შემთხვევითად',
  'configure.sideMode.challengerPicks': 'მეგობარს ვაცემ არჩევანს',
  'configure.hostSide.label': 'შენი მხარე',
  'configure.submit': 'ოთახის შექმნა',
  'configure.submitting': 'იქმნება…',
  'configure.rules.title': 'როგორ ვითამაშოთ',

  // --- Room / join ---
  'join.title': 'შემოუერთდი ოთახს',
  'join.displayName.label': 'შენი სახელი',
  'join.displayName.placeholder': 'როგორ მოგმართოთ?',
  'join.side.label': 'აირჩიე მხარე',
  'join.submit': 'შემოერთება',
  'join.submitting': 'შემოდიხარ…',
  'join.waiting': 'ველოდებით, სანამ მეგობარი ბმულს გახსნის…',
  'join.shareLink.label': 'გააზიარე ეს ბმული',
  'join.shareLink.copy': 'ბმულის კოპირება',
  'join.shareLink.copied': 'დაკოპირდა!',

  // --- Match UI ---
  'match.yourTurn': 'შენი რიგია',
  'match.opponentTurn': 'მოწინააღმდეგის რიგია',
  'match.you': 'შენ',
  'match.opponent': 'მოწინააღმდეგე',
  'match.opponentDisconnected': 'მოწინააღმდეგე გათიშულია.',
  'match.opponentReconnected': 'მოწინააღმდეგე დაბრუნდა.',
  'match.reconnecting': 'ვუკავშირდებით…',
  'match.connectionLost': 'კავშირი დაიკარგა.',
  'match.clock.label': 'საათი',
  'match.result.youWin': 'შენ მოიგე!',
  'match.result.youLose': 'შენ წააგე.',
  'match.result.draw': 'ფრე.',
  'match.result.youTimedOut': 'შენი დრო ამოიწურა.',
  'match.result.opponentTimedOut': 'მოწინააღმდეგის დრო ამოიწურა.',
  'match.rules.button': 'წესები',
  'match.rules.close': 'დახურვა',

  // --- Errors ---
  'errors.unknown': 'რაღაც შეცდომა მოხდა. სცადე თავიდან.',
  'errors.validation.displayName': 'ეს სახელი არ ვარგა.',
  'errors.validation.move': 'ეს სვლა არ ვარგა.',
  'errors.config.invalidGameId': 'უცნობი თამაში.',
  'errors.config.invalidSideSelectionMode': 'აირჩიე, როგორ მიენიჭოს მხარეები.',
  'errors.config.invalidHostSide': 'ეს მხარე აქ ვერ აირჩევა.',
  'errors.join.sideNotAllowed': 'მხარეები უკვე არჩეულია — ვერ აირჩევ.',
  'errors.join.sidePickRequired': 'აირჩიე მხარე გასაგრძელებლად.',
  'errors.join.invalidSide': 'ეს მხარე ამ თამაშისთვის არ არსებობს.',
  'errors.room.notFound': 'ეს ბმული აღარ მუშაობს ან ვადაგასულია.',
  'errors.room.alreadyJoined': 'ვიღაც უკვე შემოუერთდა ამ ოთახს.',
  'errors.room.notJoinable': 'ეს ოთახი მოთამაშეებს აღარ იღებს.',
  'errors.room.busy': 'სერვერი დაკავებულია. სცადე ცოტა მოგვიანებით.',
  'errors.move.illegalCell': 'აქ ვერ ითამაშებ.',
  'errors.move.cellOccupied': 'ეს უჯრა უკვე დაკავებულია.',
  'errors.move.notYourTurn': 'ეს შენი რიგი არ არის.',
  'errors.move.matchNotInProgress': 'მატჩი უკვე დასრულდა.',
  'errors.session.unauthorized': 'შენი სესია არასწორია. გახსენი ბმული თავიდან.',
};
