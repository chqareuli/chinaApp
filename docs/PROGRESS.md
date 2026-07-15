# Progress / Where We Left Off

Quick-glance resume pointer. Full architecture, domain model, and roadmap live in
`docs/backend-plan.md` — this file just tracks "what's done, what's next."

## Current stage: M3 done → M4 next (Entry CRUD)

Latest commit: `54316c1` — "Add JWT authentication and basic editor management (M3)"

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

### Resolved product decisions (see `docs/backend-plan.md` for full detail)
- ZH-editor status-transition scope: the broad model (Zura/GPT spec — free movement among
  new_entry/zh_review/ka_review/ready, never publish/un-publish) is authoritative. A shorter
  third-party spec proposing a narrower forward-only scope was **not** adopted.
- Status colors (gray/red/blue/green/black) recorded for the future Angular UI — no backend
  impact, just don't lose the mapping.

### Next up: M4 — Entry CRUD
- Entry create/save with the reduced Stage-1 segment shape: `Entry → Homonym → Sense →
  Definition/Example/ZhSegment/KaSegment`, with one **implicit hidden GramGrp per Homonym**
  (POS null) so the "sense must belong to a gramGrp" invariant holds without exposing
  gramGrp/POS UI yet.
- Editor main-list endpoint: status-priority + `last_modified_at desc` sort, `new_entry`
  excluded, `archived` separate.
- Search dropdown endpoint: starts-with match on `SearchNormalizedTitle`, then
  `title_length asc → status_priority asc → last_modified desc`, including the Chinese "，"
  vs regular "," comma-variant normalization.
- Optimistic concurrency via the `xmin` shadow property already wired on `Entry`.

## How to resume locally

1. Postgres running locally, database `ChinesDbGeo` (already migrated).
2. `Chiniseapp.Api`'s user-secrets must already have (set once per machine, not committed —
   see `README.md` § Local setup for the exact commands):
   - `ConnectionStrings:DefaultConnection`
   - `Jwt:Issuer` / `Jwt:Audience` / `Jwt:SigningKey` / `Jwt:AccessTokenMinutes` / `Jwt:RefreshTokenDays`
   - `Seed:SuperAdminEmail` / `Seed:SuperAdminPassword`
3. `dotnet build` from the repo root.
4. `dotnet run --project src/Chiniseapp.Api --urls "https://localhost:7010;http://localhost:5010"`
   (use https to exercise the `Secure` refresh-token cookie) → Swagger at `/swagger`.
5. `POST /auth/login` to get a bearer token; `GET /editors/me` to confirm auth works.
