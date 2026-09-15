CHINESE.GE Editorial System — Programmer Handoff Package
 (პროგრამისტებისთვის გადასაცემი სამუშაო პაკეტი)
00_READ_ME · Implementation Guide (გამოყენების ინსტრუქცია) · Version 1.0
მიზანი: ეს დოკუმენტი განსაზღვრავს, როგორ უნდა იქნეს ერთობლივად გამოყენებული Handoff-ის ფაილები. იგი არ ცვლის დეტალურ Specification-ებს ან დამტკიცებულ interactive prototype-ებს.
1. Package-ის შემადგენლობა
№
File
დანიშნულება
00
READ_ME · Implementation Guide
მოქმედების / განხორციელების სახელმძღვანელო
01
Entry_Editor_Programmer_Specification_V1.docx
სავალდებულო Lexical Data Model, Entity-ები, Cardinality, Validation, Inheritance, Stable ID-ები, Ordering და Draft/Transition rule-ები.
02
Entry_Editor_1_v1.html
დამტკიცებული Entry Editor UI/UX და Interaction prototype; Visual/Interaction reference.
03
Editorial_Panel_1_v1.html
დამტკიცებული 7 გვერდიანი Editorial Panel UI/UX prototype: Login, Home, Team, Account, Statistics/Download, Notifications, Messages.
04
Editorial_Panel_Functional_Specification_v1.docx
Editorial Panel-ის ფუნქციური ქცევის, Role permission-ების, Notification-ებისა და Entry History-ის სავალდებულო მოთხოვნები.
05
Legacy_Data_Migration_Specification_v1.docx
Legacy Dictionary-ის ახალ Semantic Model-ში გადატანისთვის სავალდებულო Migration boundary და Mapping instruction-ები.
06
Legacy_Parser_Source.xlsx
Legacy Parser/Source workbook. Migration-ის დამხმარე წყარო; არ წარმოადგენს ახალ Database Schema-ს.
07
Terminology_CANONICAL
საიტზე და პრორამებში გამოყენებული ინგლისურ-ჩინური ტერმინოლოგია CHINESE.GE — Chinese-English  HTML UI Terminology
2. დოკუმენტების პრიორიტეტი და წინააღმდეგობის გადაწყვეტა
Data structure-ის, Entity relationship-ების, Cardinality-ის, Validation-ის, Inheritance-ისა და Workflow completeness-ის საკითხებში უპირატესი ძალა აქვს Programmer Specification-ს.
Entry Editor-ის visual layout-ისა და interaction-ის საკითხებში უპირატესი ძალა აქვს Entry Editor FINAL HANDOFF HTML-ს, გარდა იმ შემთხვევისა, როდესაც იგი Data/Validation-ის ნაწილში ეწინააღმდეგება Programmer Specification-ს.
Editorial Panel-ის Functional behavior-ის, Role permission-ების, Notification-ებისა და Entry History-ის საკითხებში უპირატესი ძალა აქვს Editorial Panel Functional Specification v1-ს.
Editorial Panel-ის საერთო visual layout-ისა და interaction-ის საკითხებში, აღნიშნული შვიდი გვერდისთვის, უპირატესი ძალა აქვს Editorial Panel HTML-ს.
Legacy parsing-ისა და Migration-ის საკითხებში Migration Specification უფრო პრიორიტეტულია, ვიდრე Parser token-ებიდან ან Legacy formatting-იდან გამომდინარე ვარაუდები.
Legacy Excel workbook წარმოადგენს Migration-ის წყაროსა და დამხმარე მასალას. მასში არსებული Parser token-ები ავტომატურად არასოდეს უნდა იქნეს მიჩნეული ახალი Semantic Database Entity-ებად.
3. Implementation-ის სავალდებულო პრინციპები
პრინციპი
Implementation-ის მოთხოვნა
Human editorial control
System-მა არ უნდა განსაზღვროს, სჭირდება თუ არა Entry-ს Homograph-ები. Homograph structure-ს ქმნის და Pronunciation-ებს ანიჭებს Editor.
Semantic model
ახალი Database Entity-ები წარმოადგენს Lexical semantics-ს და არა Parser fragment-ებს. Parser segmentation ≠ Editorial data structure.
Stable identity
Reordering ცვლის მხოლოდ order-ს; Stable ID-ები არ უნდა შეიცვალოს.
Draft permissiveness
新建 (New Entry)  არის სამუშაო Draft. განსაზღვრული არასრულყოფილება იწვევს WARNING-ს და Save ნებადართული რჩება, თუ Specification პირდაპირ არ განსაზღვრავს დაუყოვნებელ ERROR-ს.
Stage transition
新建 (New Entry) → 中文审核 (CHT-review) Transition-მდე სრულდება Full structural validation; Blocking error-ები Transition-ს კრძალავს.
Controlled vocabularies
POS, Style, Domain და XR type-ები იყენებს პროექტის Controlled list-ებს; Free-text ჩანაცვლება დაუშვებელია.
Future compatibility
Audio/TTS, Example Pinyin და დახვეწილი Media subsystem ეკუთვნის Phase 2-ს და არ უნდა შეცვალოს Phase 1 Core Schema.
4. Handoff-ში შეტანილი საბოლოო UI terminology-ის შესწორებები
Entry-content search-ის მინიმალური ზღვარი: სულ მცირე 1 CH character ან სულ მცირე 2 letter.
Workflow-ის display terminology შეთანხმებულია შემდეგნაირად: 
New Entry · 新建; CHT-review · 中文审核; GEL-review · 格文审核; Ready · 待发布; Published · 已发布.
Editorial account-ის Status-ები: Active / Blocked / Cancelled.
Notifications და Messages გვერდების Filter: Only unread · 仅未读.
Statistics/Download-ის მესამე Export: Helper Materials · 辅助资料.
ახალი Entry-ის Navigation action რჩება: New Entry · 新建词条.
5. Implementation-ის თანმიმდევრობა
1. შეიქმნას ახალი Semantic Schema და Controlled Vocabulary reference-ები.
2. Entry Editor განხორციელდეს Schema-სა და Validation rule-ების შესაბამისად.
3. განხორციელდეს Workflow transition Validation და Role permission-ები.
4. განხორციელდეს Editorial Panel-ის გვერდები და Cross-page navigation.
5. Legacy data-ის ასლზე შეიქმნას Migration Parser/Transformer.
6. შესრულდეს Dry-run Migration, შეიქმნას Exception/Error report, გასწორდეს Mapping rule-ები და Migration ხელახლა გაეშვას.
7. Production cutover-მდე შესრულდეს Acceptance test-ები.
6. Change-control-ის წესი
Programmer-ებმა Lexical ambiguity არ უნდა გადაწყვიტონ Schema-ს ჩუმად შეცვლით. ნებისმიერი შემთხვევა, რომლის წარმოდგენაც დამტკიცებული Model-ით შეუძლებელია, უნდა დაფიქსირდეს Report-ში შემდეგი მონაცემებით: Legacy source sample, არსებული Parser interpretation, Proposed target structure და მიზეზი, რატომ არის დამტკიცებული Model არასაკმარისი.
