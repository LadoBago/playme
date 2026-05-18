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
  'games.tictactoe-6x6.name': 'ჯვარედინა 6×6',
  'games.tictactoe-6x6.shortDescription':
    'უფრო დიდი დაფა, უფრო გრძელი ხაზები. ვინც პირველი დააწყობს ოთხ X-ს ან O-ს — იგებს.',
  'games.tictactoe-6x6.rules':
    'მოთამაშეები რიგრიგობით სვამენ X-ს ან O-ს 6×6 დაფაზე. ვინც პირველი ჩამოაწყობს მინიმუმ ოთხ ერთსა და იმავე ნიშანს ჰორიზონტალურად, ვერტიკალურად ან დიაგონალზე — იგებს; ხუთი ან ექვსი ერთად ჩამოწყობაც გამარჯვებაა. თუ დაფა შეივსება გამარჯვებული ხაზის გარეშე — ფრე. პირველი სვლა ყოველთვის X-ს ეკუთვნის.',
  'games.tictactoe-9x9.name': 'ჯვარედინა 9×9',
  'games.tictactoe-9x9.shortDescription':
    'ხუთის გასწორება 9×9 დაფაზე. სვი X-ს ან O-ს; პირველი, ვინც სულ მცირე ხუთს დააწყობს — იგებს.',
  'games.tictactoe-9x9.rules':
    'მოთამაშეები რიგრიგობით სვამენ X-ს ან O-ს 9×9 დაფაზე. ვინც პირველი ჩამოაწყობს სულ მცირე ხუთ ერთსა და იმავე ნიშანს ზედიზედ — ჰორიზონტალურად, ვერტიკალურად ან დიაგონალზე — იგებს. უფრო გრძელი მწკრივიც (ექვსი, შვიდი, რვა ან ცხრა ზედიზედ) ერთ მოგებად ითვლება. თუ დაფა შეივსება გამარჯვებული მწკრივის გარეშე — ფრე. პირველი სვლა ყოველთვის X-ს ეკუთვნის.',
  'games.tictactoe.sideX': 'X (პირველი სვლა)',
  'games.tictactoe.sideO': 'O',
  'games.tictactoe.shortSideX': 'X',
  'games.tictactoe.shortSideO': 'O',

  'games.connect4.name': 'ოთხის გასწორება',
  'games.connect4.shortDescription':
    'ჩამოაგდე დისკები 7×6 დაფაზე გრავიტაციით. ვინც პირველი დააწყობს ოთხს — იგებს.',
  'games.connect4.rules':
    'მოთამაშეები რიგრიგობით აგდებენ წითელ და ყვითელ დისკებს 7 სვეტიან, 6 რიგიან დაფაზე. ყოველი დისკი ეცემა არჩეული სვეტის ყველაზე ქვედა ცარიელ უჯრაში. ვინც პირველი დააწყობს ოთხ თავის დისკს — ჰორიზონტალურად, ვერტიკალურად ან დიაგონალზე — იგებს. თუ დაფა შეივსება გამარჯვებული ხაზის გარეშე — ფრე. პირველი სვლა ყოველთვის წითელს ეკუთვნის.',
  'games.connect4.sideRed': 'წითელი (პირველი სვლა)',
  'games.connect4.sideYellow': 'ყვითელი',
  'games.connect4.shortSideRed': 'წითელი',
  'games.connect4.shortSideYellow': 'ყვითელი',

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
  'join.waitingForStart': 'ველოდებით მატჩის დაწყებას…',
  'join.waitingForHost': 'ველოდებით მასპინძლის დაბრუნებას…',
  'join.shareLink.label': 'გააზიარე ეს ბმული',
  'join.shareLink.copy': 'კოპირება',
  'join.shareLink.copied': 'დაკოპირდა!',
  'join.shareLink.share': 'გაზიარება…',
  'join.shareLink.shareTitle': 'PlayMe — თამაშზე მოწვევა',
  'join.shareLink.shareText.1': 'მოდი, ვითამაშოთ PlayMe-ზე.',
  'join.shareLink.shareText.2': 'შემომიერთდი PlayMe-ზე.',
  'join.shareLink.shareText.3': 'გელოდები PlayMe-ზე.',
  'join.shareLink.shareText.4': 'ვითამაშოთ PlayMe-ზე!',
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
  'match.result.youResigned': 'შენ დანებდი.',
  'match.result.opponentResigned': 'მოწინააღმდეგე დაგნებდა.',
  'match.result.youDisconnected': 'შენი კავშირი დაიკარგა.',
  'match.result.opponentDisconnected': 'მოწინააღმდეგის კავშირი დაიკარგა.',
  'match.resign.button': 'დანებება',
  'match.resign.confirm.title': 'ნამდვილად გსურთ დანებება?',
  'match.resign.confirm.body': 'ამ მატჩს წააგებ.',
  'match.resign.confirm.yes': 'დანებება',
  'match.resign.confirm.cancel': 'გაუქმება',
  'match.backToLobby': 'მთავარ გვერდზე დაბრუნება',
  'match.opponentLeft': 'მოწინააღმდეგე გავიდა.',
  'match.rematch.offer.button': 'რევანშის შეთავაზება',
  'match.rematch.accept.button': 'რევანშის მიღება',
  'match.rematch.reject.button': 'უარის თქმა',
  'match.rematch.waiting': 'ველოდებით მოწინააღმდეგეს…',
  'match.rematch.offered': 'მოწინააღმდეგე გთავაზობს რევანშს.',
  'match.rematch.declined': 'მოწინააღმდეგემ უარი თქვა რევანშზე.',
  'match.rematch.confirmReject.title': 'ნამდვილად გსურთ რევანშზე უარის თქმა?',
  'match.rematch.confirmReject.body': 'მთავარ გვერდზე დაბრუნდები.',
  'match.rematch.confirmReject.yes': 'უარი',
  'match.rematch.confirmReject.cancel': 'გაუქმება',
  'match.score.label': 'ანგარიში',
  'match.score.draws.one': '1 ფრე',
  'match.score.draws.other': '{count} ფრე',
  'match.rules.button': 'წესები',
  'match.rules.close': 'დახურვა',
  'match.board.label': 'თამაშის დაფა',
  'match.board.cell.label': 'მწკრივი {row}, სვეტი {col}',

  // --- Connect 4 a11y labels (per-module vocab) ---
  'games.connect4.board.label': 'Connect 4-ის დაფა',
  'games.connect4.columns.top': 'Connect 4 სვეტები (ზემოდან)',
  'games.connect4.columns.bottom': 'Connect 4 სვეტები (ქვემოდან)',
  'games.connect4.dropColumn': 'ჩამოაგდე დისკი სვეტში {col}',
  'games.connect4.cell.discRed': 'წითელი დისკი',
  'games.connect4.cell.discYellow': 'ყვითელი დისკი',
  'games.connect4.cell.empty': 'ცარიელი უჯრა, მწკრივი {row}',

  // --- App-level error / not-found chrome ---
  'errors.boundary.title': 'რაღაც შეცდომა მოხდა.',
  'errors.boundary.retry': 'სცადე თავიდან',
  'notFound.home': '← მთავარი',

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
  'errors.exit.notAllowed': 'ახლა გასვლა შეუძლებელია.',
  'errors.rematch.invalidState': 'ახლა რევანშის შეთავაზება შეუძლებელია.',
  'errors.rematch.notResponder': 'მხოლოდ შენს მოწინააღმდეგეს შეუძლია მიიღოს ან უარყოს.',
};
