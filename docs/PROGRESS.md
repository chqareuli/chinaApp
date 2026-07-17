# Progress / Where We Left Off

Quick-glance resume pointer. Full architecture, domain model, and roadmap live in
`docs/backend-plan.md` — this file just tracks "what's done, what's next."

## Current stage: M5 done → M6 next (Scoring + accounting)

Latest work: M5 — Status workflow. See `git log --oneline` for the exact hash.

### Done
- **M1** — solution scaffold (`Chiniseapp.Domain/Application/Infrastructure/Api` + `Tests`),
  EF Core + Npgsql wiring, `/health/db` connectivity check.
- **M2** — full Stage-1 domain model (`Entry`, `Segment` tree, `ControlledVocabulary`,
  `Comment`, `ReferenceMaterial`, `Editor`, `ScoreEntry`, `Notification`, `DirectMessage`,
  `AuditLogEntry`, `MediaAsset`), EF configurations, native Postgres enums, `InitialCoreSchema`
  migration applied to local Postgres.
- **M3** — JWT auth (login/refresh/logout), role-gated `[Authorize(Roles=...)]`, live
  deactivation revocation, bootstrap `super_admin` seeding, `EditorsController`.
- **M4** — Entry CRUD against the reduced Stage-1 shape (`Entry → Homonym → Sense →
  Definition/Example/ZhSegment/KaSegment`, implicit hidden `GramGrp`). Create, full-replace
  content save (transactional), get (tree reconstruction), main list, search — all verified live.
- **M5** — Status workflow: `StatusTransitionRules` (pure, exhaustively unit-tested against the
  resolved role×status table), `POST /entries/{id}/status`, `MainAuthor` lock (to
  `CreatedByEditorId`, on first exit from `new_entry`), `AuditLogEntry` on every transition.
  Verified live through a full entry lifecycle new_entry→zh_review→ka_review→ready→published
  with the correct actors, plus every forbidden-transition case (ka_editor skipping straight to
  ready, zh_editor trying to publish or move a published entry backward).

### Resolved product decisions (see `docs/backend-plan.md` for full detail)
- ZH-editor status-transition scope: the broad model (free movement among
  new_entry/zh_review/ka_review/ready, never publish/un-publish) is authoritative — implemented
  and locked in by `StatusTransitionRulesTests`.
- Status colors (gray/red/blue/green/black) recorded for the future Angular UI — no backend
  impact, just don't lose the mapping.

### Known simplifications / gaps (intentional, revisit later — not oversights)
- **M4**: content save is full-replace (segment ids not stable across saves); no gramGrp/POS
  UI; no IE elements (XR/STYLE/DOMAIN) yet, `Definition`/`KaSegment` are plain text. All Stage 2.
- **M5**: `PUT /entries/{id}/content` authorization is still a fixed role list, not
  status-aware — e.g. once an entry reaches `ka_review`, only `ka_editor` should be able to
  edit it, but that narrowing isn't implemented. Worth doing alongside or right after M6/M7.
- Main-list/search visibility is not yet role-scoped (spec says KA Editor should see only
  `ka_review` entries, Assistant Editor only assigned/new ones on their main view) — everyone
  currently sees everything via `GET /entries` and `/search`.

### Next up: M6 — Scoring + accounting
- `ScoringService` hooked into `ChangeStatusAsync`'s status-change event (same call site the
  comment in `EntryService.ChangeStatusAsync` already flags): main score to `MainAuthorEditorId`
  once, the first time an entry leaves `new_entry`; additional score to any other contributing
  editor, once per entry; separate ka_editor score for `ka_review` work.
- `ScoreEntry` unique index (`entry_id, editor_id, score_type`) already enforces "once" at the
  DB level (from M2) — the service just needs to catch the unique-violation / check-then-insert.
- Accounting endpoints: global editor list with totals, entry-status totals, personal
  accounting page (main vs additional counts by status).

## How to resume locally

1. Postgres running locally, database `ChinesDbGeo` (still on the M2 migration — M3/M4/M5 added
   no new schema).
2. `Chiniseapp.Api`'s user-secrets must already have (set once per machine, not committed —
   see `README.md` § Local setup for the exact commands):
   - `ConnectionStrings:DefaultConnection`
   - `Jwt:Issuer` / `Jwt:Audience` / `Jwt:SigningKey` / `Jwt:AccessTokenMinutes` / `Jwt:RefreshTokenDays`
   - `Seed:SuperAdminEmail` / `Seed:SuperAdminPassword`
3. `dotnet build` from the repo root.
4. `dotnet run --project src/Chiniseapp.Api --urls "https://localhost:7010;http://localhost:5010"`
   (use https to exercise the `Secure` refresh-token cookie) → Swagger at `/swagger`.
5. `POST /auth/login` to get a bearer token; try `POST /entries`, `PUT /entries/{id}/content`,
   `POST /entries/{id}/status`.

**Shell gotcha (not a bug):** testing with Git Bash/curl and inline CJK/Georgian text in `-d`
or bare query-string arguments can mangle UTF-8 before it reaches curl. Write JSON payloads to
a file and use `--data-binary @file`, and percent-encode non-ASCII query params
(e.g. `为` → `%E4%B8%BA`) when testing from that shell.

**Local test accounts** already seeded in the dev DB from manual testing (not in any migration
or seed script — recreate if working from a fresh database):
`admin@chinese.ge` (super_admin), `zura@chinese.ge` (zh_editor), `nino@chinese.ge` (ka_editor).
