// Georgian (ka) catalog. Sprint 6 wires the full ka/en switcher and
// completes the translations; Sprint 1 ships an initial pass mirroring the
// en keys 1:1 so the catalog files don't drift in shape.

import type { EnKey } from './en';

export const ka: Record<EnKey, string> = {
  // --- Site chrome ---
  'site.title': 'PlayMe — ითამაშე ონლაინ მეგობართან, რეგისტრაცია არ გჭირდება',
  'site.titleShort': 'PlayMe — ითამაშე ონლაინ მეგობართან',
  'site.titleSuffix': '— PlayMe',
  'site.tagline': 'ითამაშე ონლაინ მეგობართან, რეგისტრაცია არ გჭირდება.',
  'site.homeMetaDescription':
    'ითამაშე სამაგიდო, ინტელექტუალური თამაშები ონლაინ მეგობართან — რეგისტრაციის გარეშე. აირჩიე თამაში და გააზიარე ბმული. თამაში უფასოა.',
  'site.brandTagline': 'შენი ჯერია.',
  'site.ogImageAlt': 'PlayMe — ითამაშე ონლაინ მეგობართან, რეგისტრაცია არ გჭირდება.',
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
  'games.tictactoe.name': 'ჯვარ-ნული',
  'games.tictactoe.metaTitle': 'ჯვარ-ნული ონლაინ — Tic Tac Toe | PlayMe',
  'games.tictactoe.metaDescription':
    'ითამაშე ჯვარ-ნული (X და 0, იქსიკი და ნოლიკი) ონლაინ მეგობართან — 3×3, 6×6 ან 9×9 დაფაზე. რეგისტრაცია არ გჭირდება.',
  'games.tictactoe.shortDescription':
    'აირჩიე დაფა: 3×3, 6×6 ან 9×9. ვინც პირველი დააწყობს მითითებულ რაოდენობას — იგებს.',
  'games.tictactoe.rules':
    'მოთამაშეები რიგრიგობით სვამენ X-ს და O-ს დაფაზე. 3×3 დაფაზე გასამარჯვებლად საჭიროა სამის დაწყობა; 6×6 დაფაზე — მინიმუმ ოთხის; 9×9 დაფაზე — მინიმუმ ხუთის. ხაზები ითვლება ჰორიზონტალურად, ვერტიკალურად ან დიაგონალზე. დიდ დაფებზე უფრო გრძელი მწკრივიც ერთ მოგებად ითვლება. თუ დაფა შეივსება გამარჯვებული ხაზის გარეშე — ფრე. პირველი სვლა ყოველთვის X-ს ეკუთვნის.',
  'games.tictactoe.sideX': 'X (პირველი სვლა)',
  'games.tictactoe.sideO': 'O',
  'games.tictactoe.shortSideX': 'X',
  'games.tictactoe.shortSideO': 'O',

  'games.connect4.name': 'ოთხის გასწორება',
  'games.connect4.metaTitle': 'ოთხის გასწორება ონლაინ — Connect 4 (ოთხი ზედიზედ) | PlayMe',
  'games.connect4.metaDescription':
    'ითამაშე Connect 4 (ოთხი ზედიზედ) ონლაინ მეგობართან — ჩამოაგდე დისკები და დააწყე პირველმა ოთხი ქვა ერთ ხაზზე. რეგისტრაცია არ გჭირდება.',
  'games.connect4.shortDescription':
    'ჩამოაგდე დისკები 7×6 დაფაზე გრავიტაციით. ვინც პირველი დააწყობს ოთხს — იგებს.',
  'games.connect4.rules':
    'მოთამაშეები რიგრიგობით აგდებენ წითელ და ყვითელ დისკებს 7 სვეტიან, 6 რიგიან დაფაზე. ყოველი დისკი ეცემა არჩეული სვეტის ყველაზე ქვედა ცარიელ უჯრაში. ვინც პირველი დააწყობს ოთხ თავის დისკს — ჰორიზონტალურად, ვერტიკალურად ან დიაგონალზე — იგებს. თუ დაფა შეივსება გამარჯვებული ხაზის გარეშე — ფრე. პირველი სვლა ყოველთვის წითელს ეკუთვნის.',
  'games.connect4.sideRed': 'წითელი (პირველი სვლა)',
  'games.connect4.sideYellow': 'ყვითელი',
  'games.connect4.shortSideRed': 'წითელი',
  'games.connect4.shortSideYellow': 'ყვითელი',

  'games.reversi.name': 'რევერსი',
  'games.reversi.metaTitle': 'რევერსი ონლაინ — Reversi / ოტელო (Othello) | PlayMe',
  'games.reversi.metaDescription':
    'ითამაშე რევერსი (ოტელო, Reversi) ონლაინ მეგობართან 8×8 დაფაზე — მოაქციე და გადააბრუნე მოწინააღმდეგის დისკები. რეგისტრაცია არ გჭირდება.',
  'games.reversi.shortDescription':
    'მოწინააღმდეგის დისკები მოაქციე შენებს შორის და გადააქციე შენიანებად. ვინც დასასრულს მეტ დისკს ფლობს — იგებს.',
  'games.reversi.rules':
    'მოთამაშეები რიგრიგობით სვამენ შავ და თეთრ დისკებს 8×8 დაფაზე; თამაშს შავები იწყებენ. პირველი ოთხი სვლა აუცილებლად ცენტრალურ 2×2 უჯრებში სრულდება და ჯერ არცერთი დისკი არ ბრუნდება — ეს კლასიკური თავისუფალი დებიუტია. მე-5 სვლიდან აუცილებელია, ყოველმა დადებულმა დისკმა მოწინააღმდეგის მინიმუმ ერთი დისკი მაინც მოაქციოს შენს დისკებს შორის სწორ ხაზზე (ჰორიზონტალურად, ვერტიკალურად ან დიაგონალზე); სვლის შედეგად მოწინააღმდეგის ყველა დისკი რომელიც მოექცა შენს დისკებს შორის, ტრიალდება და შენი დისკების ფერის ხდება. თუ კანონიერი სვლა არ გაქვს, სვლას ავტომატურად ტოვებ. მატჩი მთავრდება, როცა დაფა შეივსება ან ორივე მხარე ზედიზედ გამოტოვებს სვლას. გამარჯვებულია ის, ვისაც მეტი დისკი აქვს; თანაბარი რაოდენობის დისკების შემთხვევაში ფრეა.',
  'games.reversi.sideDark': 'შავი (პირველი სვლა)',
  'games.reversi.sideLight': 'თეთრი',
  'games.reversi.shortSideDark': 'შავებით',
  'games.reversi.shortSideLight': 'თეთრებით',

  // --- Configure page ---
  'configure.title': 'ოთახის კონფიგურაცია',
  'configure.displayName.label': 'შენი სახელი',
  'configure.displayName.placeholder': 'როგორ მოგმართო?',
  'configure.sideMode.label': 'მხარეების მინიჭება',
  'configure.sideMode.hostPicksSpecific': 'მე ავირჩევ ჩემს მხარეს',
  'configure.sideMode.random': 'შემთხვევითად',
  'configure.sideMode.challengerPicks': 'მეგობარს ვაძლევ უფლებას აირჩიოს',
  'configure.hostSide.label': 'შენი მხარე',
  'configure.boardSize.label': 'დაფის ზომა',
  'configure.submit': 'ოთახის შექმნა',
  'configure.submitting': 'იქმნება…',
  'configure.rules.title': 'როგორ ვითამაშოთ',
  'configure.back': 'მთავარ გვერდზე დაბრუნება',

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
  'match.backToLobby': 'მთავარ გვერდზე',
  'match.backToLobby.short': 'დაბრუნება',
  'match.opponentLeft': 'მოწინააღმდეგე გავიდა.',
  'room.expired.title': 'ოთახი ვადაგასულია',
  'room.expired.body': '30 წუთის განმავლობაში არავინ შემოვიდა, ეს ოთახი წაიშალა. ახალის შესაქმნელად დაბრუნდი მთავარზე.',
  'room.expired.cta': '← მთავარი',
  'match.rematch.offer.button': 'რევანშის შეთავაზება',
  'match.rematch.offer.short': 'რევანში',
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
  'match.score.wins.one': '1 მოგება',
  'match.score.wins.other': '{count} მოგება',
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

  // --- Reversi a11y labels (per-module vocab) ---
  'games.reversi.board.label': 'რევერსის დაფა',
  'games.reversi.cell.discDark': 'შავი დისკი',
  'games.reversi.cell.discLight': 'თეთრი დისკი',
  'games.reversi.cell.empty': 'ცარიელი უჯრა, მწკრივი {row}, სვეტი {col}',
  'games.reversi.cell.legal': 'კანონიერი სვლა, მწკრივი {row}, სვეტი {col}',
  'games.reversi.score.dark': '{count} შავი',
  'games.reversi.score.light': '{count} თეთრი',
  'games.reversi.toast.autoPassSelf': 'სვლა არ გაქვს, კვლავ მისი სვლაა.',
  'games.reversi.toast.autoPassOpponent': 'მოწინააღმდეგეს არ აქვს სვლა, კვლავ შენ სვლაა.',

  // --- PWA install prompt (Sprint 6) ---
  'pwa.install.title': 'დააყენე PlayMe როგორც აპლიკაცია',
  'pwa.install.cta': 'დაყენება',
  'pwa.install.dismiss': 'დახურვა',

  // --- Locale toggle (Sprint 6) ---
  'locale.switch.toKa': 'გადართე ქართულზე',
  'locale.switch.toEn': 'გადართე ინგლისურზე',

  // --- Theme toggle (Sprint 6) ---
  'theme.toggle.next.dark': 'გადართე მუქ თემაზე',
  'theme.toggle.next.system': 'გადართე სისტემურ თემაზე',
  'theme.toggle.next.light': 'გადართე ნათელ თემაზე',

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
  'errors.config.invalidGameOptions': 'თამაშის პარამეტრები არასწორია.',
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
  'errors.move.outOfBounds': 'ეს ადგილი დაფის გარეთ არის.',
  'errors.move.mustBracket': 'უნდა მოაქცე მინიმუმ ერთი მოწინააღმდეგის დისკი შენებს შორის.',
  'errors.move.openingMustBeCentral': 'საწყისი სვლები ცენტრალურ ოთხ უჯრაში სრულდება.',
  'errors.move.passNotAllowed': 'სვლის გამოტოვება არ შეიძლება — კანონიერი სვლა გაქვს.',
  'errors.move.notYourTurn': 'შენი რიგი არ არის.',
  'errors.move.matchNotInProgress': 'მატჩი უკვე დასრულდა.',
  'errors.session.unauthorized': 'შენი სესია არასწორია. გახსენი ბმული თავიდან.',
  'errors.rate.exceeded': 'მეტისმეტად სწრაფად ცდილობ. დაიცადე და სცადე თავიდან.',
  'errors.exit.notAllowed': 'ახლა გასვლა შეუძლებელია.',
  'errors.rematch.invalidState': 'ახლა რევანშის შეთავაზება შეუძლებელია.',
  'errors.rematch.notResponder': 'მხოლოდ შენს მოწინააღმდეგეს შეუძლია მიიღოს ან უარყოს.',
};
