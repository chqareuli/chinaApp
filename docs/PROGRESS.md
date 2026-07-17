# Progress / Where We Left Off

Quick-glance resume pointer. Full architecture, domain model, and roadmap live in
`docs/backend-plan.md` — this file just tracks "what's done, what's next."

## Current stage: M6 done → M7 next (Notifications + direct messaging)

Latest work: M6 — Scoring + accounting. See `git log --oneline` for the exact hash.

### Done
- **M1** — solution scaffold, EF Core + Npgsql wiring, `/health/db` connectivity check.
- **M2** — full Stage-1 domain model, EF configurations, native Postgres enums,
  `InitialCoreSchema` migration applied to local Postgres.
- **M3** — JWT auth (login/refresh/logout), role-gated authorization, live deactivation
  revocation, bootstrap `super_admin` seeding, `EditorsController`.
- **M4** — Entry CRUD against the reduced Stage-1 shape, full-replace content save, main list,
  search, optimistic concurrency.
- **M5** — Status workflow: `StatusTransitionRules` (exhaustively unit-tested),
  `POST /entries/{id}/status`, `MainAuthor` lock, `AuditLogEntry` on every transition.
- **M6** — Scoring (`IScoringService`: Main once per entry to the author, Additional once per
  non-author editor who touches it, dedicated KaEditor score for ka_review completions) hooked
  into `SaveContentAsync`/`ChangeStatusAsync` so awards commit atomically with the triggering
  change. Accounting (`IAccountingService`): global per-editor totals, entry-status totals,
  personal page grouped by entries' current status. Resolved Q5 privacy rule enforced
  (KA Editor / Assistant Editor: own page only, no global list). Verified live: promoter vs.
  author distinction, once-per-entry dedup across repeated actions, and both sides of the Q5
  restriction.

### Resolved product decisions (see `docs/backend-plan.md` for full detail)
- ZH-editor status-transition scope: broad model, implemented and locked in by tests.
- Status colors recorded for the future Angular UI — no backend impact.
- Q5 (accounting page privacy): KA Editor and Assistant Editor may only view their own
  accounting page; everyone else can view everyone's — implemented in `AccountingController`.

### Open item flagged, not silently decided
- The ka_editor score's "final score type" is explicitly marked unresolved in the source spec.
  Implemented as its own `ScoreType.KaEditor` bucket (matches the M2 schema) rather than folding
  it into Additional — tell the assistant if you want it changed.

### Known simplifications / gaps (intentional, revisit later — not oversights)
- **M4**: content save is full-replace (segment ids not stable across saves); no gramGrp/POS
  UI; no IE elements yet, `Definition`/`KaSegment` are plain text. All Stage 2.
- **M5**: `PUT /entries/{id}/content` authorization is still a fixed role list, not
  status-aware (e.g. ka_editor should gain ka_review-scoped edit rights once an entry reaches
  that status).
- Main-list/search visibility is not role-scoped yet (spec: KA Editor should see only
  `ka_review` entries on their main view, Assistant Editor only assigned/new ones) — everyone
  currently sees everything via `GET /entries` and `/search`.

### Next up: M7 — Notifications + direct messaging
- On every entry change (content save or status change), notify `main_author` + `last_editor`
  + everyone who ever edited/commented on the entry, excluding whoever just made the change.
  Natural hook point: same call sites as M6's scoring (`SaveContentAsync`/`ChangeStatusAsync`).
  Needs a way to know "everyone who ever touched this entry" — `AuditLogEntry` rows and/or
  `ScoreEntry` rows (Additional-score recipients) are the closest existing proxy; may need a
  dedicated `EntryContributor` concept, or derive from those.
- Notification must link back to the entry (`Notification` entity already has `EntryId`).
- Plain `DirectMessage` CRUD (send/receive/mark-read) — entity already exists from M2, no new
  schema needed.
- Super Admin notification-management ("mute/unmute someone's notifications") is flagged
  Future/Stage 2 in the spec — skip for M7.

## How to resume locally

1. Postgres running locally, database `ChinesDbGeo` (still on the M2 migration — no schema
   changes through M6).
2. `Chiniseapp.Api`'s user-secrets must already have (set once per machine, not committed —
   see `README.md` § Local setup):
   `ConnectionStrings:DefaultConnection`, `Jwt:*`, `Seed:SuperAdminEmail`/`SuperAdminPassword`.
3. `dotnet build` from the repo root.
4. `dotnet run --project src/Chiniseapp.Api --urls "https://localhost:7010;http://localhost:5010"`
   (https needed for the `Secure` refresh-token cookie) → Swagger at `/swagger`.
5. `POST /auth/login`, then try `POST /entries`, `PUT /entries/{id}/content`,
   `POST /entries/{id}/status`, `GET /accounting/editors`, `GET /accounting/editors/{id}`.

**Shell gotcha (not a bug):** Git Bash/curl with inline CJK/Georgian text in `-d` or bare
query-string args can mangle UTF-8. Write JSON payloads to a file and use `--data-binary @file`;
percent-encode non-ASCII query params (e.g. `为` → `%E4%B8%BA`).

**Local test accounts** already seeded in the dev DB from manual testing (not in any migration
or seed script — recreate if working from a fresh database):
`admin@chinese.ge` (super_admin, id 1), `zura@chinese.ge` (zh_editor, id 2),
`nino@chinese.ge` (ka_editor, id 3), password `Passw0rd!` for zura/nino.
