# HTML დიზაინის გადატანა — მიმდინარე მდგომარეობა

წყაროები: `docs/design-reference/Editorial Panel_v1.html` და `Entry_Editor_v1.html`. ZIP-ის HTML და დოკუმენტები გამოყენებულია დიზაინისა და ქცევის მითითებად. დოკუმენტებში ნახსენები production cutover და ძველი კორპუსის მიგრაცია ავტომატურად არ შესრულებულა.

## განხორციელებულია

- HTML-ის CSS და SVG ხატულები გადატანილია Angular-ის პანელში. ზედა ნავიგაცია, მთავარი ცხრილი, გუნდი, ანგარიში, სტატისტიკა, ნოტიფიკაციები, მიმოწერა და სტატიის რედაქტორი ახალ სტილზეა.
- მოქმედებს ავტორის/რედაქტორის/სტატუსის ფილტრები, სრული ტექსტის ძიება, pagination, სტატისტიკიდან გაფილტრულ სიაზე გადასვლა და შრიფტის ზომის კონტროლი.
- სტატიის რედაქტორი რეალურ ASP.NET API-სთან ინახავს მრავალ პინინს, ტრადიციულ ფორმებს, ჰომონიმებს, გრამატიკულ ჯგუფებს, POS-ს, მნიშვნელობებს, მაგალითებს, ნიშნებსა და cross-reference-ებს. ძველი Homonyms/Senses ფორმატი კვლავ იკითხება.
- არსებული ელემენტების რედაქტირება და გადალაგება ინარჩუნებს მათ segment ID-ებს; შენარჩუნებული ელემენტის სხვა ჯგუფში გადატანა კომენტარის სამიზნეს არ წყვეტს. ელემენტის განზრახ წაშლისას კომენტარი რჩება სტატიის დონეზე.
- შენახვა ტრანზაქციულია და ამოწმებს rowVersion-ს. მოძველებული ვერსიით შენახვისას არ იცვლება არც ტექსტი და არც ელემენტების სტრუქტურა. უცნობი ზედა JSON ველი უარყოფილია. გამოქვეყნება ბექენდშიც ამოწმებს აუცილებელ POS-ს, მნიშვნელობებსა და განმარტებებს.
- ისტორია, წაუკითხავი შეტყობინებების რაოდენობა, საკუთარი სახელისა და პაროლის შეცვლა დაკავშირებულია ბექენდთან.
- დამხმარე მასალებს აქვს ცალკე მუდმივი შენახვა, ენების არჩევა და ავტორის/Supervisor-ის წაშლის უფლებები. ეს მასალა არ ერწყმის სტატიის ტექსტს.
- მედიის ატვირთვა, ნახვა და წაშლა მუშაობს. დაშვებულია PNG/JPEG/WebP, მაქსიმუმ 5 MB. ფაილები ინახება API-ის `App_Data/media` დირექტორიაში, გენერირებული სახელებით; წვდომა მოწმდება სტატიის უფლებებით. ეს დირექტორია Git-ში არ შედის და განთავსებისას მუდმივ საცავსა და backup-ს საჭიროებს.
- სტატისტიკას დაემატა HTML-ის Database Download ბლოკი: სტატუსი, რედაქტორი, From/To და TEI/XML, JSON, Helper Materials. წვდომა მხოლოდ Supervisor-ს აქვს როგორც UI-ში, ისე endpoint-ზე.
- ექსპორტი იყენებს `UpdatedAtUtc`-ს და UTC კალენდარულ დღეებს, დასასრულის დღის ჩათვლით. NewEntry და Archived გამორიცხულია. თითო ჩამოტვირთვაზე მაქსიმუმ 5,000 სტატიაა; უფრო დიდი ნაკრებისთვის საჭიროა ფილტრით დაყოფა. ჩვეულებრივი ექსპორტი არ შეიცავს შიდა კომენტარებს, ანგარიშის ინფორმაციას ან დამხმარე მასალებს. TEI/XML შემოწმებულია well-formed XML-ზე და ტექსტის/კავშირების შენარჩუნებაზე; კონკრეტულ TEI customization/schema-სთან სრული ვალიდაცია არ ჩატარებულა.

## ბაზა და გაშვება

2026-09-07-ს განახლდა კონფიგურაციაში მითითებული **ლოკალური PostgreSQL**. გამოყენებულია არსებული pending როლებისა და ქულების migration, ახალი `lexical_forms` ველი და Editorial Attachments migration. არსებული ლოკალური media `entry_id`/index/FK გათვალისწინებულია migration-ში. Production სერვერზე ცვლილება ან deploy არ განხორციელებულა.

- API: `dotnet run --project src/Chiniseapp.Api` — პორტი 5010, Development-ის არსებული user-secrets.
- მხოლოდ migration: `dotnet run --project src/Chiniseapp.Api -- --migrate-only`.
- ფრონტენდი: `frontend/editorial-panel`-ში `npm run start:local`.
- სამუშაო მისამართი: `http://127.0.0.1:4303/#/adminpanel`. გამოიყენება ნამდვილი ლოკალური API; საჭიროა არსებული ანგარიშით შესვლა.
- Mock გარემო კვლავ ცალკეა: `npm run mock-api` და `npm run start:qa` (4300). ძველი დემოს ნებისმიერ პაროლზე შესვლა რეალურ API-ზე არ მოქმედებს.

## შემოწმება

- Angular: 69/69 ტესტი, production build წარმატებულია გაფრთხილებების გარეშე.
- .NET: 276/276 ტესტი, მათ შორის რეალური PostgreSQL-ის ინტეგრაციული ტესტი. ტესტი ქმნის ცალკე დროებით ბაზას, იყენებს migration-ებს და დასრულებისას შლის მხოლოდ ამ დროებით ბაზას.
- PostgreSQL-ზე შემოწმებულია შენახვა/ხელახლა ჩატვირთვა, პინინისა და ტრადიციული ფორმების შენარჩუნება, ჯგუფში გადატანისას კომენტარის ID-ის შენარჩუნება და stale-write rollback.
- რეალურ HTTP API-ზე: login, მთავარი სია, გუნდი, სტატისტიკა, unread counts და სამივე ექსპორტი აბრუნებს წარმატებულ პასუხს. დროებით QA სტატიაზე შემოწმებულია ფორმის JSON payload, helper add/delete, image upload/read/delete და history/comments/media/helper list. QA სტატია და სურათი გასუფთავებულია.
- წინა ვიზუალური შემოწმება მოიცავდა პანელის გვერდებს, რედაქტორს და 390px სიგანის მიმოწერის/რედაქტორის ფორმებს. მიმდინარე რეალური პანელის შესვლის გვერდიც გაიხსნა ბრაუზერში.

PostgreSQL ტესტის გასაშვებად საჭიროა `CHINESE_TEST_POSTGRES` გარემოს ცვლადი ლოკალური სერვერის connection string-ით. სხვა შემთხვევაში ეს ერთი ინტეგრაციული ტესტი გამოტოვებულია და ეს შედეგში ჩანს.

## ზუსტი შესაბამისობის საზღვრები

ახალი რედაქტორის რეალურ ბაზაში შენახვის ბლოკერი მოგვარებულია. თუმცა **სრული პიქსელური და ახალი სპეციფიკაციის სრული ფუნქციური 1:1 შესაბამისობა ჯერ არ არის დადასტურებული**:

- შესვლა ინარჩუნებს არსებული API-ის Email-ს. 2026-09-15: გუნდის დამატება/რედაქტირება გადატანილია HTML-ის ინგლისურ-ჩინურ ფორმებზე; ინახება სახელი, გვარი, ჩინური სახელი და WeChat. სარედაქციო როლი და Supervisor ცალ-ცალკე აისახება; მხარდაჭერილია Cancelled და მაქსიმუმ ორი აქტიური Supervisor. დროებითი პაროლის შეცვლის ტექსტი ნიმუშიდანაა; პირველ შესვლაზე ცვლილების იძულებითი მექანიზმი ჯერ არ დანერგილა.
- ნავიგაციაში All entries შენარჩუნებულია; ცალკე საერთო Media ბიბლიოთეკის გვერდი არ შექმნილა. მედია ხელმისაწვდომია სტატიის შიგნით.
- შენარჩუნებულია არსებული მოქმედებები, ზოგი ქართული ტექსტი და დამატებითი ანგარიშის/ქულების მონაცემები.
- Pronunciation–Homograph კავშირის სრული ახალი მოდელი, inline rich-text, ყველა inheritance წესი და ძველი კორპუსის canonical მოდელში სრული მიგრაცია ცალკე დარჩენილი სამუშაოა.

დიზაინის საწყისი CSS-ის იმპორტი: `node tools/import-design-reference.mjs`. Angular-ის ადაპტაციებია `src/handoff-adapters.scss` და კომპონენტების შაბლონები.

## გუნდის გვერდის შემოწმება — 2026-09-15

დამატებისა და რედაქტირების ფორმები შემოწმდა რეალურ ბრაუზერში. Angular-ის 69 ტესტი და production build წარმატებულია; .NET-ის 276 ტესტი წარმატებულია. PostgreSQL-ის იზოლირებულ ტესტში დამატებით დადასტურდა ახალი პროფილის ველებისა და Supervisor-სგან დამოუკიდებელი სარედაქციო როლის შენახვა/წაკითხვა. ლოკალურ ბაზაზე გამოყენებულია AddEditorTeamProfile migration. არსებული ანგარიშების მონაცემები შენარჩუნებულია.


## მიმოწერა და ნოთიფიკაციები — 2026-09-15

HTML-ის მიხედვით განახლდა ახალი მიმოწერის არჩევა (საუბარი იხსნება არჩევისთანავე), SVG ხატულები, შავი გაგზავნის ღილაკი, ავტორის როლის ნიშნები, წაუკითხავი/არჩეული სტრიქონების ფერები და Quick View. საერთო ძიება ამ გვერდებზე ფილტრავს მათ შიგთავსს. ნავიგაციის აქტიური ხატულა იყენებს ნიმუშის ლურჯ ფონს.

All editors ინახება საერთო იდენტიფიკატორით და თითო აქტიური მიმღების ცალკე ასლით. თითოეული მიმღები მხოლოდ საკუთარ წაკითხვის მდგომარეობას ცვლის; პირადი წერილები საერთო საუბარში არ ხვდება. მიმღებები განისაზღვრება გაგზავნის მომენტში. ახალი ანგარიშები წარსულ საერთო წერილებს ავტომატურად არ იღებენ.

ახალი კომენტარისა და სტატუსის ცვლილების ნოთიფიკაციებს ინახება დეტალი. ძველ ჩანაწერებში დაკარგული დეტალი არ გამოიგონება: Quick View აჩვენებს ცვლილების ტიპსა და სტატიის მიმდინარე სტატუსს. სტატუსის ფერები მიმდინარე მდგომარეობას ასახავს. HTML-ის გამოგონილი შეტყობინებები/რაოდენობები რეალურ ბაზაში არ გადმოტანილა.

შემოწმება: Angular 72/72, .NET 277/277; production build წარმატებულია. იზოლირებული PostgreSQL ტესტი ამოწმებს საერთო/პირადი წერილების განცალკევებას, თითო მიმღების წაკითხვას, სხვისი წერილის/ნოთიფიკაციის წაკითხულად მონიშვნის უარყოფას და Quick View-ის მონაცემებს. მომხმარებლებისთვის სატესტო წერილები არ გაგზავნილა. რეალურ ბრაუზერში შემოწმდა ორივე გვერდი, მიმღების არჩევა და Quick View. ლოკალურად გამოყენებულია AddBroadcastMessagesAndNotificationDetails migration; API და frontend გაშვებულია.

## პროფილი — 2026-09-15

My Account-ის ვიზუალი გადატანილია ნიმუშის MY WORK / PERSONAL INFORMATION / SECURITY სტრუქტურაზე. ცხრილს აქვს Entries/Main score/Additional score და ხუთი ეტაპი, Total-ით. Entries ითვლის რედაქტორის შექმნილ ან ქულით დაკავშირებულ უნიკალურ არაარქივირებულ სტატიებს, New Entry-ების ჩათვლით. ქულები მოდის შენახული ScoreEntries-დან; ჯამი ინარჩუნებს არქივირებულ სტატიებზე მიღებულ ქულებსაც. თითო რიცხვი უკავშირდება შესაბამის workEditorId/scoreKind/status ფილტრიან სიას. მოქმედი როლების წვდომის შეზღუდვები შენარჩუნებულია.

პირად ფორმაში ინახება სახელი, გვარი, ჩინური სახელი, Email და WeChat; Email-ის კონფლიქტი მოწმდება. ძველ ანგარიშებზე, რომლებსაც ცალკე სახელი/გვარი არ აქვთ, ფორმა აჩვენებს displayName-ის დაყოფას; ბაზაში ეს მხოლოდ Save-ის შემდეგ ინახება. პაროლის შეცვლა ამოწმებს მიმდინარე პაროლს და ახალი მნიშვნელობების შესაბამისობას. მომხმარებლის რეალური პაროლი/Email შემოწმებისას არ შეცვლილა.

Angular 75/75, .NET 278/278; build წარმატებულია. ახალი იზოლირებული PostgreSQL ტესტი ადარებს პროფილის რაოდენობებს შესაბამის სიებს და ამოწმებს არქივში გადასვლისას ქულების შენარჩუნებას. ბრაუზერში შემოწმდა პროფილი და 15 New Entry-ის ბმულიდან ზუსტად 15 ჩანაწერის გახსნა. ქულების მინიჭების არსებული ბიზნესწესები ამ ვიზუალური ცვლილებით არ შეცვლილა; EP-ACC-ის ყველა წესის სრული აუდიტი ცალკე რჩება.

## სტატიის დამატების სტრუქტურა — 2026-09-15

Entry_Editor_v1.html-ის მიხედვით გადაეწყო საწყისი +H/+POS/+S არჩევანი, H1/H2 ერთხაზიანი სათაურები, POS-ის რიგი, Sense-ის განმარტებისა და ინსტრუმენტების განლაგება და Example-ის ორი ენობრივი მხარე. პირველი +H ქმნის ორ ცარიელ ბლოკს; რედაქტორი თავად ირჩევს შემდეგ დონეს. Style/Domain და სხვა ნიშნები პირდაპირ controlled-vocabulary select-ებით ირჩევა; Style/Domain-ში დუბლიკატები და ორზე მეტი არჩევანი არ ემატება.

Homograph-ის პირდაპირი Sense ინახება directSenses-ში, შესაბამისი Sense segment-ით უშუალოდ Homograph-ის ქვეშ. შენარჩუნებულია ძველი gramGroups/Senses თავსებადობა. პირდაპირი Sense გათვალისწინებულია ხელახალ ჩატვირთვაში, Preview-ში, გამოქვეყნების ვალიდაციასა და TEI ექსპორტში. ახალი migration არ სჭირდება.

Angular 76/76, .NET 278/278; production build წარმატებულია CSS budget გაფრთხილების გარეშე. PostgreSQL ტესტმა დაადასტურა ორი Homograph-ის პირდაპირი Sense-ის შენახვა/ხელახლა ჩატვირთვა და ID-ის შენარჩუნება. ბრაუზერში შემოწმდა +H, Style-ის არჩევა, +POS და nested Sense. რეალურ ბაზაში სანახავი ცარიელი სტატია არ შენახულა.

სრული canonical სპეციფიკაციის დარჩენილი ნაწილები (Pronunciation-ის ID კავშირი, runtime-derived Homograph label, inheritance, ყველა inline rich-text/media placement ქცევა) ამ ვიზუალური ცვლილებით სრულად დანერგილად არ ითვლება. არსებული label და marker მონაცემები შენარჩუნებულია; შინაარსიანი ორი Homograph-იდან ერთის წაშლა დაბლოკილია, თუ დარჩენილი სტრუქტურა ერთ Homograph-ს დატოვებდა — ავტომატური სემანტიკური კონვერტაცია არ სრულდება.

### Entry editor action bar — 2026-09-15
Replaced the Frames checkbox with the reference toggle button and persisted on/off preference. Frames now only hide hierarchy borders, preserving backgrounds/content. Added initial read-only mode, Edit, confirmed Cancel restoring saved nested content, and Save exiting edit mode only on success. Removed the editor Preview action and its unused mapping; the reference has no Preview button. Browser checked Edit and frame border/background behavior. Angular 77 tests and production build pass. Remaining mismatch: stage transitions retain the existing separate confirmation flow rather than the prototype's save-with-stage transaction; broader canonical editor behavior remains tracked above.

### H/POS/S/Example and media comparison — 2026-09-15
Compared reference templates and later override scripts with the technical specification. Corrected H max 10; root POS/direct senses can be wrapped into H1 with H2 added while preserving controls; blocked direct-sense/POS mixing under a Homograph; preserved final implicit POS sense; added duplicate sibling POS client save validation. Single grammar numeral hidden, single direct sense S, single example E. Added numbered-definition-line tool and per-Sense/Example image button. Image dialog uses existing authenticated MediaService upload with stable sense:id/example:id placement; attachments render under their associated block. Unsaved blocks explicitly require saving first. Uploads are immediate, separate from content Cancel; only JPEG/PNG/WebP up to 5 MB, matching server. Reference itself only simulates media attachments and defers final media subsystem design. Frontend 81 tests pass, including upload service arguments/returned attachment and hierarchy preservation. Production build passes. Browser verified POS/example media dialog on an unsaved block; no live upload was made. Not a claim of complete canonical implementation: pronunciation-linked H labels, inheritance, full rich-text tools, automatic H collapse and stage transaction remain broader gaps.

### Definition/example layout and text size — 2026-09-15
Added A−/A+ at the reference positions: definition field / 34px font controls / 270px tools; examples have two equal columns plus 34px controls, with reference mobile stacking. Definition tools now use two rows (Style, Domain, numbered point; Link, media, Abbr); example tools form one row. Existing selectors and lookup behavior preserved. Text size changes by 1px within 12–40px; example controls resize Chinese and Georgian together. Text content is unchanged. Removed conflicting component grid overrides. Single sense badge S and root scope frame restored. Frontend 82 tests pass; build passed before final badge/class-only changes. Browser verified A+ and tool grid positions at the narrow panel viewport; full-width screenshot equivalence was not measured.
