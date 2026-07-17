# Progress / Where We Left Off

Quick-glance resume pointer. Full architecture, domain model, and roadmap live in
`docs/backend-plan.md` — this file just tracks "what's done, what's next."

## Current stage: M8a done → M8b next (legacy-import tool, blocked on data)

Latest work: M8a — Comments + Reference Material CRUD. See `git log --oneline` for the hash.

### Done
- **M1** — solution scaffold, EF Core + Npgsql wiring, `/health/db` connectivity check.
- **M2** — full Stage-1 domain model, EF configurations, native Postgres enums,
  `InitialCoreSchema` migration applied to local Postgres.
- **M3** — JWT auth, role-gated authorization, live deactivation revocation, bootstrap
  `super_admin` seeding, `EditorsController`.
- **M4** — Entry CRUD against the reduced Stage-1 shape, full-replace content save, main list,
  search, optimistic concurrency.
- **M5** — Status workflow, `StatusTransitionRules` (exhaustively unit-tested), `MainAuthor`
  lock, `AuditLogEntry` on every status change.
- **M6** — Scoring (Main/Additional/KaEditor) and accounting endpoints, resolved Q5 privacy
  rule (KA/Assistant Editor: own accounting page only).
- **M7** — Notifications on every content save/status change, fanned out to everyone who ever
  worked on the entry, excluding the actor (resolved Q4). Direct messaging.
- **M8a** — `Comment` CRUD (`CommentsController`): list/add per entry, edit/archive gated to
  the comment's author or Chief Editor/Super Admin. Adding a comment awards Additional score
  and notifies past contributors, same as editing content. `ReferenceMaterial` get/save
  (`ReferenceMaterialController`, G1–G5, 1:1 with Entry). Added `AuditAction.CommentAdded`
  (new migration, `ALTER TYPE audit_action ADD VALUE`). Verified live: scoring/notification
  fan-out from a comment, non-owner-non-privileged edit/archive rejected (403), privileged
  override works, multi-script (EN/ZH/RU/KA) reference material round-trips correctly.

### Resolved product decisions (see `docs/backend-plan.md` for full detail)
- ZH-editor status-transition scope: broad model, implemented and locked in by tests.
- Status colors recorded for the future Angular UI — no backend impact.
- Q5 (accounting privacy): KA/Assistant Editor see only their own accounting page.
- Q4 (self-notifications): not sent.
- M8 scope split: build Comment/ReferenceMaterial CRUD now; defer the legacy-import console
  tool until real old-database data or access is available (explicit product-owner decision).

### Open item flagged, not silently decided
- The ka_editor score's "final score type" is explicitly unresolved in the source spec.
  Implemented as its own `ScoreType.KaEditor` bucket — tell the assistant if you want it changed.

### Known simplifications / gaps (intentional, revisit later — not oversights)
- **M4**: content save is full-replace (segment ids not stable across saves); no gramGrp/POS
  UI; no IE elements yet. All Stage 2.
- **M5**: `PUT /entries/{id}/content` authorization is still a fixed role list, not
  status-aware.
- Main-list/search visibility is not role-scoped yet (KA Editor should see only `ka_review`
  entries on their main view, Assistant Editor only assigned/new ones).
- **M7**: notification "type" is just the raw `AuditAction` name, no richer summary text yet.
  Message-to-entry linkification and Super Admin notification mute/unmute are Stage 2 per spec.
- **M8a**: `Comment.TargetSegmentId` stays null always (entry-level only) — segment-level
  comments are a Stage 2 concept once the full Editorial Panel segment tree exists.

### Next up: M8b — Legacy-import console tool (blocked on data)
**Needs from the product owner before this can start meaningfully**: either a sample export
(or DB access) of the old database's comment and reference-material blob fields, so the parser
can be built and tested against real formatting quirks rather than guessed examples.

Once unblocked:
- Separate console tool project (`tools/Chiniseapp.LegacyImport`, per the original M1 plan —
  not built yet), not part of the running API, since it's a one-shot ETL.
- Comment parser: split on `&Author&` shift markers (text after one belongs to that author
  until the next marker or field end) and `#...#` → `editor_note_ka` spans (rendered blue in
  the old UI). Always preserve `Comment.OriginalRawComment` unmodified alongside the
  best-effort `ParsedCommentParts` jsonb — never discard the raw text, even on partial parse
  failure (this rule is already reflected in the M2 schema/docs, just not exercised by real
  data yet).
- Reference-material parser: split on `**`/`***`/`****`/`*****` delimiters into G1–G5,
  `//`/`///` → bullet markers. Preserve `OriginalRawReferenceMaterial` alongside the split
  fields (schema already supports this from M2/M8a — `ReferenceMaterialService.SaveAsync`
  currently always writes an empty string there since it's only used for manually-authored
  rows so far).

## How to resume locally

1. Postgres running locally, database `ChinesDbGeo`, migrated through
   `20260717194626_AddCommentAddedAuditAction` (run `dotnet ef database update` if unsure).
2. `Chiniseapp.Api`'s user-secrets must already have (set once per machine, not committed —
   see `README.md` § Local setup):
   `ConnectionStrings:DefaultConnection`, `Jwt:*`, `Seed:SuperAdminEmail`/`SuperAdminPassword`.
3. `dotnet build` from the repo root.
4. `dotnet run --project src/Chiniseapp.Api --urls "https://localhost:7010;http://localhost:5010"`
   (https needed for the `Secure` refresh-token cookie) → Swagger at `/swagger`.
5. `POST /auth/login`, then try `POST /entries/{id}/comments`, `GET /entries/{id}/comments`,
   `PUT /entries/{id}/reference-material`.

**Shell gotcha (not a bug):** Git Bash/curl with inline CJK/Georgian text in `-d` or bare
query-string args can mangle UTF-8. Write JSON payloads to a file and use `--data-binary @file`;
percent-encode non-ASCII query params (e.g. `为` → `%E4%B8%BA`).

**Local test accounts** already seeded in the dev DB from manual testing (not in any migration
or seed script — recreate if working from a fresh database):
`admin@chinese.ge` (super_admin, id 1), `zura@chinese.ge` (zh_editor, id 2),
`nino@chinese.ge` (ka_editor, id 3), password `Passw0rd!` for zura/nino.
