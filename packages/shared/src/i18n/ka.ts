// Georgian (ka) catalog. Sprint 6 wires the full ka/en switcher and
// completes the translations; Sprint 1 ships an initial pass mirroring the
// en keys 1:1 so the catalog files don't drift in shape.

import type { EnKey } from './en';

export const ka: Record<EnKey, string> = {
  // --- Site chrome ---
  'site.title': 'PlayMe — ითამაშე მეგობართან, რეგისტრაცია არ გჭირდება',
  'site.titleSuffix': '— PlayMe',
  'site.tagline': 'ითამაშე მეგობართან, რეგისტრაცია არ გჭირდება.',
  'site.ogImageAlt': 'PlayMe — ითამაშე მეგობართან, რეგისტრაცია არ გჭირდება.',
  'site.howItWorks.title': 'როგორ მუშაობს PlayMe',
  'site.howItWorks.step1.title': 'აირჩიე თამაში',
  'site.howItWorks.step1.body':
    'აირჩიე თამაში და დააფიქსირე შენი მხარე. ანგარიში არ გჭირდება.',
  'site.howItWorks.step2.title': 'გააზიარე ბმული',
  'site.howItWorks.step2.body':
    'მიიღებ უნიკალურ ბმულს. გაუგზავნე ვისთანაც გინდა ითამაშო.',
  'site.howItWorks.step3.title': 'ითამაშე ცოცხლად',
  'site.howItWorks.step3.body':
    'პირველი მეგობარი, რომელიც ბმულს გახსნის, შემოგიერთდება. ითამაშე რიგრიგობით.',
  'site.catalog.title': 'აირჩიე თამაში',

  // --- Game names + rules ---
  'games.tictactoe-3x3.name': 'ჯვარედინა 3×3',
  'games.tictactoe-3x3.shortDescription':
    'კლასიკა. პირველი, ვინც 3-ს მწკრივში, სვეტში ან დიაგონალზე ერთად დააწყობს — იგებს.',
  'games.tictactoe-3x3.rules':
    'მოთამაშეები რიგრიგობით სვამენ X-ს ან O-ს 3×3 დაფაზე. ვინც პირველი ჩამოაწყობს სამ ერთსა და იმავე ნიშანს ჰორიზონტალურად, ვერტიკალურად ან დიაგონალზე — იგებს. თუ დაფა შეივსება გამარჯვებული მწკრივის გარეშე — ფრე. პირველი სვლა ყოველთვის X-ს ეკუთვნის.',
  'games.tictactoe.sideX': 'X (პირველი სვლა)',
  'games.tictactoe.sideO': 'O',

  'games.connect4.name': 'ოთხის გასწორება',
  'games.connect4.shortDescription':
    'ჩამოაგდე დისკები 7×6 დაფაზე გრავიტაციით. ვინც პირველი დააწყობს ოთხს — იგებს.',
  'games.connect4.rules':
    'მოთამაშეები რიგრიგობით აგდებენ წითელ და ყვითელ დისკებს 7 სვეტიან, 6 რიგიან დაფაზე. ყოველი დისკი ეცემა არჩეული სვეტის ყველაზე ქვედა ცარიელ უჯრაში. ვინც პირველი დააწყობს ოთხ თავის დისკს — ჰორიზონტალურად, ვერტიკალურად ან დიაგონალზე — იგებს. თუ დაფა შეივსება გამარჯვებული ხაზის გარეშე — ფრე. პირველი სვლა ყოველთვის წითელს ეკუთვნის.',
  'games.connect4.sideRed': 'წითელი (პირველი სვლა)',
  'games.connect4.sideYellow': 'ყვითელი',

  // --- Configure page ---
  'configure.title': 'ოთახის კონფიგურაცია',
  'configure.displayName.label': 'შენი სახელი',
  'configure.displayName.placeholder': 'როგორ მოგმართო?',
  'configure.sideMode.label': 'მხარეების მინიჭება',
  'configure.sideMode.hostPicksSpecific': 'მე ავირჩევ ჩემს მხარეს',
  'configure.sideMode.random': 'შემთხვევითად',
  'configure.sideMode.challengerPicks': 'მეგობარს ვაძლევ უფლებას აირჩიოს',
  'configure.hostSide.label': 'შენი მხარე',
  'configure.submit': 'ოთახის შექმნა',
  'configure.submitting': 'იქმნება…',
  'configure.rules.title': 'როგორ ვითამაშოთ',

  // --- Room / join ---
  'join.title': 'შემოუერთდი ოთახს',
  'join.displayName.label': 'შენი სახელი',
  'join.displayName.placeholder': 'როგორ მოგმართო?',
  'join.side.label': 'აირჩიე მხარე',
  'join.submit': 'შემოერთება',
  'join.submitting': 'შემოდიხარ…',
  'join.waiting': 'ველოდებით, სანამ მეგობარი ბმულს გახსნის…',
  'join.shareLink.label': 'გააზიარე ეს ბმული',
  'join.shareLink.copy': 'ბმულის კოპირება',
  'join.shareLink.copied': 'დაკოპირდა!',
  'join.invite.headline': 'შენ მოწვეული ხარ თამაშზე',
  'join.invite.host': 'მასპინძელი',
  'join.invite.game': 'თამაში',
  'join.invite.rules': 'წესები',

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
  'errors.move.illegalColumn': 'ასეთი სვეტი არ არსებობს.',
  'errors.move.columnFull': 'ეს სვეტი სავსეა.',
  'errors.move.notYourTurn': 'ეს შენი რიგი არ არის.',
  'errors.move.matchNotInProgress': 'მატჩი უკვე დასრულდა.',
  'errors.session.unauthorized': 'შენი სესია არასწორია. გახსენი ბმული თავიდან.',
  'errors.rate.exceeded': 'მეტისმეტად სწრაფად ცდილობ. დაიცადე და სცადე თავიდან.',
};
