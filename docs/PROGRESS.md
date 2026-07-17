# Progress / Where We Left Off

Quick-glance resume pointer. Full architecture, domain model, and roadmap live in
`docs/backend-plan.md` — this file just tracks "what's done, what's next."

## Current stage: M4 done → M5 next (Status workflow)

Latest commit: (M4 — Entry CRUD) — see `git log --oneline` for the exact hash.

### Done
- **M1** — solution scaffold (`Chiniseapp.Domain/Application/Infrastructure/Api` + `Tests`),
  EF Core + Npgsql wiring, `/health/db` connectivity check.
- **M2** — full Stage-1 domain model (`Entry`, `Segment` tree, `ControlledVocabulary`,
  `Comment`, `ReferenceMaterial`, `Editor`, `ScoreEntry`, `Notification`, `DirectMessage`,
  `AuditLogEntry`, `MediaAsset`), EF configurations, native Postgres enums, `InitialCoreSchema`
  migration applied to local Postgres.
- **M3** — JWT auth (login/refresh/logout), role-gated `[Authorize(Roles=...)]`, live
  deactivation revocation (checked on every request, not just at token issuance), bootstrap
  `super_admin` seeding from user-secrets, `EditorsController` (me/list/create/
  deactivate/reactivate/reset-password). Verified end-to-end against a running local Postgres.
- **M4** — Entry CRUD against the reduced Stage-1 shape (`Entry → Homonym → Sense →
  Definition/Example/ZhSegment/KaSegment`, one implicit hidden `GramGrp` per homonym).
  `POST /entries` (headword-only create), `PUT /entries/{id}/content` (full-replace save,
  transactional so a concurrency conflict can't half-apply), `GET /entries/{id}` (tree
  reconstruction), `GET /entries` (main list: `status_priority` asc + `updated_at_utc` desc,
  `new_entry` excluded), `GET /entries/search?q=` (starts-with, `title_length asc → status
  priority asc → last_modified desc`, includes `new_entry`, Chinese/regular comma-variant
  normalization). Optimistic concurrency via `xmin` → `rowVersion`, 409 on stale save. New
  `Domain/Rules/EntryStatusPriority` and `TitleNormalizer`, unit-tested. Verified live
  end-to-end (create, save, round-trip get, 409 on stale save, list excludes new_entry, search
  includes it and sorts by length, KaEditor correctly rejected from creating entries).

### Resolved product decisions (see `docs/backend-plan.md` for full detail)
- ZH-editor status-transition scope: the broad model (Zura/GPT spec — free movement among
  new_entry/zh_review/ka_review/ready, never publish/un-publish) is authoritative. A shorter
  third-party spec proposing a narrower forward-only scope was **not** adopted.
- Status colors (gray/red/blue/green/black) recorded for the future Angular UI — no backend
  impact, just don't lose the mapping.

### Known M4 simplifications (intentional, revisit in Stage 2)
- Content save is **full-replace**: every `PUT .../content` deletes all of an entry's segments
  and re-creates them from the request. Segment ids are not stable across saves. Fine for
  Stage 1 (small articles, entry-level comments only); once Stage 2's Comments/XR need to target
  a *specific* segment, this needs to become a real diff/partial-update.
- `gramGrp`/`POS` exist in the schema (`SegmentType` enum) but are never exposed — every
  homonym silently gets one hidden `GramGrp`. Stage 2 exposes multi-gramGrp/POS authoring.
- No IE elements (XR/STYLE/DOMAIN/Abbr/Lang) yet — `Definition`/`KaSegment` are plain text
  (`Value`), not the mixed-content `Content` jsonb shape described in the Editorial Panel spec.

### Next up: M5 — Status workflow
- `StatusTransitionRules` in Domain (pure, unit-testable) encoding the resolved role×status
  table from `docs/backend-plan.md` § Status workflow.
- `POST /entries/{id}/status` endpoint, gated by that table.
- `MainAuthor` lock logic: set once when an entry first leaves `new_entry` (assistant-authored
  entries promoted by a supervisor keep the assistant as `main_author`).
- Audit log entries (`AuditLogEntry`, `AuditAction.StatusChanged`) on every transition.

## How to resume locally

1. Postgres running locally, database `ChinesDbGeo` (already migrated — no new migration was
   needed for M4, the M2 schema already covered it).
2. `Chiniseapp.Api`'s user-secrets must already have (set once per machine, not committed —
   see `README.md` § Local setup for the exact commands):
   - `ConnectionStrings:DefaultConnection`
   - `Jwt:Issuer` / `Jwt:Audience` / `Jwt:SigningKey` / `Jwt:AccessTokenMinutes` / `Jwt:RefreshTokenDays`
   - `Seed:SuperAdminEmail` / `Seed:SuperAdminPassword`
3. `dotnet build` from the repo root.
4. `dotnet run --project src/Chiniseapp.Api --urls "https://localhost:7010;http://localhost:5010"`
   (use https to exercise the `Secure` refresh-token cookie) → Swagger at `/swagger`.
5. `POST /auth/login` to get a bearer token; `GET /editors/me` to confirm auth works;
   `POST /entries` + `PUT /entries/{id}/content` to try the new Entry CRUD.

**Shell gotcha (not a bug):** testing with Git Bash/curl and inline CJK/Georgian text in `-d`
or bare query-string arguments can mangle UTF-8 before it reaches curl. Write JSON payloads to
a file and use `--data-binary @file`, and percent-encode non-ASCII query params
(e.g. `为` → `%E4%B8%BA`) when testing from that shell.
