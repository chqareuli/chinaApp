# Progress / Where We Left Off

Quick-glance resume pointer. Full architecture, domain model, and roadmap live in
`docs/backend-plan.md` — this file just tracks "what's done, what's next."

## Current stage: M7 done → M8 next (Comments + Reference Material + legacy import)

Latest work: M7 — Notifications + direct messaging. See `git log --oneline` for the exact hash.

### Done
- **M1** — solution scaffold, EF Core + Npgsql wiring, `/health/db` connectivity check.
- **M2** — full Stage-1 domain model, EF configurations, native Postgres enums,
  `InitialCoreSchema` migration applied to local Postgres.
- **M3** — JWT auth, role-gated authorization, live deactivation revocation, bootstrap
  `super_admin` seeding, `EditorsController`.
- **M4** — Entry CRUD against the reduced Stage-1 shape, full-replace content save, main list,
  search, optimistic concurrency.
- **M5** — Status workflow: `StatusTransitionRules` (exhaustively unit-tested), status endpoint,
  `MainAuthor` lock, `AuditLogEntry` on every status change.
- **M6** — Scoring (Main/Additional/KaEditor, once per entry) and accounting endpoints, with the
  resolved Q5 privacy rule (KA/Assistant Editor: own accounting page only).
- **M7** — Notifications on every content save or status change, fanned out to everyone who
  ever worked on the entry (derived from `AuditLogEntry` — `SaveContentAsync` now logs
  `ContentEdited` too, closing a gap where only status changes were audited), excluding the
  actor (resolved Q4: no self-notifications). Plain `DirectMessage` CRUD via a separate
  `MessagesController`. Verified live: self-exclusion, fan-out to past contributors, cross-user
  mark-read rejected (404), message round-trip for both sender and recipient.

### Resolved product decisions (see `docs/backend-plan.md` for full detail)
- ZH-editor status-transition scope: broad model, implemented and locked in by tests.
- Status colors recorded for the future Angular UI — no backend impact.
- Q5 (accounting privacy): KA/Assistant Editor see only their own accounting page.
- Q4 (self-notifications): not sent — implemented in `NotifyEntryChangedAsync`.

### Open item flagged, not silently decided
- The ka_editor score's "final score type" is explicitly unresolved in the source spec.
  Implemented as its own `ScoreType.KaEditor` bucket — tell the assistant if you want it
  changed (e.g. folded into Additional).

### Known simplifications / gaps (intentional, revisit later — not oversights)
- **M4**: content save is full-replace (segment ids not stable across saves); no gramGrp/POS
  UI; no IE elements yet. All Stage 2.
- **M5**: `PUT /entries/{id}/content` authorization is still a fixed role list, not
  status-aware (e.g. ka_editor should gain ka_review-scoped edit rights).
- Main-list/search visibility is not role-scoped yet (KA Editor should see only `ka_review`
  entries on their main view, Assistant Editor only assigned/new ones).
- **M7**: notification "type" is just the raw `AuditAction` name (`ContentEdited`/
  `StatusChanged`) — no richer `change_summary` text yet (spec's pseudocode mentions one).
  Message-to-entry linkification (auto-link a Chinese word in a message body to its dictionary
  entry, spec §7 Stage 2 item) is not built — explicitly deferred to Stage 2 in the source docs.
  Super Admin per-editor notification mute/unmute is Future/Stage 2 per spec — skipped.

### Next up: M8 — Comments + Reference Material + legacy import
- `Comment` CRUD scoped to an entry (entity already exists from M2: `TargetSegmentId` nullable,
  always null in Stage 1). Comments are internal-only — never exposed in any public/entry
  content response.
- `ReferenceMaterial` read endpoint (G1–G5, entity already exists from M2).
- Legacy-import console tool (separate from the running API, per the original plan) that
  parses the old database's raw comment blobs (`&Author&` shift markers, `#...#` → Georgian
  editor notes, rendered blue in the old UI) and reference-material blobs (`**`/`***`/`****`/
  `*****` delimiters splitting G1–G5, `//`/`///` → bullet markers) — preserving the original
  raw text alongside the best-effort parsed structure, per the spec's explicit "never discard
  the original" requirement. This is the first M8 item that needs real legacy data/access to
  build against meaningfully — confirm with the product owner what's available before starting.

## How to resume locally

1. Postgres running locally, database `ChinesDbGeo` (still on the M2 migration — no schema
   changes through M7).
2. `Chiniseapp.Api`'s user-secrets must already have (set once per machine, not committed —
   see `README.md` § Local setup):
   `ConnectionStrings:DefaultConnection`, `Jwt:*`, `Seed:SuperAdminEmail`/`SuperAdminPassword`.
3. `dotnet build` from the repo root.
4. `dotnet run --project src/Chiniseapp.Api --urls "https://localhost:7010;http://localhost:5010"`
   (https needed for the `Secure` refresh-token cookie) → Swagger at `/swagger`.
5. `POST /auth/login`, then try `GET /notifications`, `POST /messages`, `GET /messages`.

**Shell gotcha (not a bug):** Git Bash/curl with inline CJK/Georgian text in `-d` or bare
query-string args can mangle UTF-8. Write JSON payloads to a file and use `--data-binary @file`;
percent-encode non-ASCII query params (e.g. `为` → `%E4%B8%BA`).

**Local test accounts** already seeded in the dev DB from manual testing (not in any migration
or seed script — recreate if working from a fresh database):
`admin@chinese.ge` (super_admin, id 1), `zura@chinese.ge` (zh_editor, id 2),
`nino@chinese.ge` (ka_editor, id 3), password `Passw0rd!` for zura/nino.
