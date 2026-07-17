# Backend Architecture & Roadmap

Source specs this plan is derived from (editorial admin platform spec, raw requirements notes,
and the detailed "Editorial Panel" structured-entry spec) live outside this repo with the
product owner; this document distills the decisions that shape the backend.

## Scope

- **Backend only.** The Angular admin panel ("Editorial Panel") and the public dictionary site
  are built separately, later, and are not part of this repo.
- **Phased delivery.** Stage 1 preserves the legacy program's statuses/roles/workflow, but
  stores entries as genuine structured data (not a raw-text blob) so it is a real subset of
  Stage 2, never a throwaway prototype. Stage 2 adds the full IE/XR/STYLE/DOMAIN richness, TEI
  export, and media library.

## Target architecture

```
chiniseapp.sln
 src/Chiniseapp.Domain/          net8.0, zero external deps — entities, enums, pure rules
 src/Chiniseapp.Application/     net8.0, refs Domain — services, interfaces, DTOs
 src/Chiniseapp.Infrastructure/  net8.0, refs Domain+Application — EF Core, Npgsql, repositories
 src/Chiniseapp.Api/             net8.0 ASP.NET Core, refs Application+Infrastructure — composition root
 tests/Chiniseapp.Tests/         xUnit, refs everything
```

Reference direction: `Api → Application → Domain`; `Infrastructure → Application + Domain`.
`Api` touches `Infrastructure` only for DI registration (`AddInfrastructure(...)`), never for
business logic directly. Domain stays dependency-free because entry-tree validation and
status-transition rules are complex and worth unit-testing without a DbContext.

## Domain model (target shape, built incrementally from M2)

- **Entry**: Id, Lemma, Pinyin, Status, StatusPriority, CreatedByEditorId, MainAuthorEditorId,
  LastEditorEditorId, CreatedAtUtc, UpdatedAtUtc, SearchNormalizedTitle (comma-variant
  normalized), IsFlaggedForRepair, LegacyRawCommentBlob, RowVersion (Postgres `xmin`).
- **Segment** (self-referencing tree): Id, EntryId, ParentSegmentId, SegmentType (Homonym/
  GramGrp/Pos/Sense/Definition/Example/ZhSegment/KaSegment/Xr/Style/Domain/Abbr/Lang),
  OrderIndex, Number (system-generated gramGrp/sense numbering), Value (plain leaf text),
  Content (jsonb mixed-content array for Definition/KaSegment — text runs + `segmentRef`s into
  inline-IE child segments), Attributes (jsonb, IE-specific payload).
- **ControlledVocabulary**: reference table (not enums) for POS (~15 codes), STYLE, DOMAIN,
  Abbr, Lang — admin-curated, needs no deploy to change.
- **Comment**: EntryId, TargetSegmentId (nullable, always null in Stage 1), AuthorEditorId,
  CommentText, OriginalRawComment (legacy import, immutable), ParsedCommentParts (jsonb —
  `&Author&` marker + `#...#` → editor_note_ka spans, best-effort, never discards the raw text).
- **ReferenceMaterial** (1:1 Entry): OriginalRawReferenceMaterial, G1..G5 (NOT NULL, empty
  string when absent), split on legacy `**`/`***`/`****`/`*****` delimiters, `//`/`///` → bullet
  markers.
- **Editor** (Identity-backed): Role (SuperAdmin/ChiefEditor/ZhEditor/KaEditor/AssistantEditor),
  IsActive (block, never hard-delete), LegacyRoleRaw (historical metadata only).
- **ScoreEntry**: EntryId, EditorId, ScoreType (Main/Additional/KaEditor), unique
  (EntryId, EditorId, ScoreType) — enforces "once per entry" at the DB level.
- **Notification** / **DirectMessage**, **AuditLogEntry** (doubles as status history),
  **MediaAsset** (Stage 2+, entity reserved now).

Enum strategy: workflow enums (EntryStatus, EditorRole, SegmentType, IeType, Placement,
ScoreType, CommentStatus, AuditAction) → **native Postgres enum types** via Npgsql
(`MapEnum<T>()` + `HasPostgresEnum<T>()`) since they gate business logic and must never be free
text. Larger/admin-curated vocabularies (POS/STYLE/DOMAIN/Abbr/Lang) → the
`ControlledVocabulary` table instead of enums.

Key indexes (from M2): `Segments(EntryId)`, `Segments(ParentSegmentId)`,
`Segments(EntryId, ParentSegmentId, OrderIndex)`, `Entries(StatusPriority, UpdatedAtUtc)`
(partial, `WHERE status <> 'new_entry'`, for the editor-list sort),
`Entries(SearchNormalizedTitle)` with `text_pattern_ops` for the starts-with search sort
(title_length asc → status_priority asc → last_modified desc).

**Stage-1 tree-shape rule**: implement the *full* `SegmentType` enum/table from M2 so Stage 2
never needs a schema rewrite, but Stage-1 services only exercise the reduced node set
(Entry → Homonym → Sense → Definition/Example/ZhSegment/KaSegment) and **auto-create one
implicit GramGrp per Homonym** (POS null, hidden) to keep the "sense must belong to a gramGrp"
invariant true without exposing gramGrp/POS UI yet.

## Cross-cutting decisions

- **Auth** (implemented M3): hand-rolled JWT bearer auth around the plain `Editor` entity —
  `PasswordHasher<Editor>` used standalone for hashing (no `UserManager`/`IdentityDbContext`),
  short-lived access token + stateless longer-lived refresh token in an httpOnly/Secure cookie.
  Role kept as a typed enum column (drives the status-transition table directly in C#) and
  emitted as a claim for `[Authorize(Roles=)]`. `IsActive` is re-checked against the DB on every
  request (not just at token issuance), so deactivation takes effect immediately.
- **Audit logging**: a generic `SaveChangesInterceptor` (catches all tracked changes) plus
  explicit semantic calls from services for meaningful events (StatusChanged, RoleChanged).
- **Side effects** (scoring/notifications/audit on status change): plain **service-layer
  orchestration** in one DB transaction — no MediatR/domain-event bus, for a 1-developer team.
- **Local dev**: Postgres runs locally; the connection string lives in `dotnet user-secrets`
  for `Chiniseapp.Api`, never in `appsettings.json`.

## Stage 1 API surface (built across M3–M8)

`AuthController`, `EditorsController`, `EntriesController` (list/search/get/create/save,
`POST /entries/{id}/status` gated by the role×status transition table), `AccountingController`,
`NotificationsController`, `MessagesController` (built as two controllers instead of one,
for cleaner routing), `CommentsController`, `ReferenceMaterialController`, `VocabController`,
`AuditController`.

## Status workflow (resolved role × transition rules)

A second, shorter spec (from a third team member) described a narrower ZH-editor scope
(`new_entry → zh_review → ka_review` only, forward-only). Product owner decision: **the
original detailed spec below is authoritative**; the narrower version is not implemented. Revisit
only if explicitly requested again.

| From status | To status | Super Admin | Chief Editor | ZH Editor | KA Editor | Assistant Editor |
|---|---|---|---|---|---|---|
| new_entry | zh_review / ka_review / ready / published | Yes | Yes | Yes | No | No |
| new_entry | zh_review / higher, *by a supervisor promoting an assistant's entry* | Yes | Yes | Yes | No | No (may remain `main_author`) |
| zh_review | new_entry / ka_review / ready | Yes | Yes | Yes | No | No |
| zh_review | published | Yes | Yes | No | No | No |
| ka_review | zh_review | Yes | Yes | Yes | Yes | No |
| ka_review | ready | Yes | Yes | Yes | Yes | No |
| ka_review | published | Yes | Yes | No | No | No |
| ready | zh_review / ka_review | Yes | Yes | Yes (not from published) | No | No |
| ready | published | Yes | Yes | No | No | No |
| published | any lower status | Yes | Yes | No | No | No |
| any | archived / problem flag | Yes | Yes | No | No | No |

Summary: ZH Editor may move an entry freely among `new_entry`/`zh_review`/`ka_review`/`ready` in
either direction, but never publishes and never moves a published entry backward. KA Editor may
only move `ka_review ↔ zh_review` and `ka_review → ready`. Only Chief Editor/Super Admin publish,
un-publish, or archive. Assistant Editor never changes status directly.

### Status colors (reference for the future Angular UI — no backend impact)

| Status | Label (ka) | Color |
|---|---|---|
| new_entry | ახალი სტატია | gray |
| zh_review | ჩინური შემოწმება | red |
| ka_review | ქართული შემოწმება | blue |
| ready | მზადაა | green |
| published | გამოქვეყნებული | black |
| archived | არქივი | *(not specified yet)* |

## Phased roadmap

- **M1 (done)** — solution scaffold, EF Core+Npgsql wiring, empty `ChiniseDbContext`,
  `/health/db` connectivity smoke check.
- **M2 (done)** — Postgres enums, `ControlledVocabulary`, `Editor`, `Entry`, `Segment` and the
  remaining Stage-1 entities (`Comment`, `ReferenceMaterial`, `ScoreEntry`, `Notification`,
  `DirectMessage`, `AuditLogEntry`, `MediaAsset`) + configurations/indexes; first migration
  (`InitialCoreSchema`) applied to local Postgres. `Editor` is a plain entity, not
  `IdentityUser<int>` — Domain stays dependency-free; ASP.NET Identity/JWT plumbing is real M3
  work, not just a type it inherits from.
- **M3 (done)** — Hand-rolled JWT auth around the plain `Editor` entity: `PasswordHasherService`
  wraps ASP.NET Core Identity's `PasswordHasher<Editor>` standalone (PBKDF2, no
  `UserManager`/`IdentityDbContext` — Editor stays a lean custom entity, not `IdentityUser<int>`,
  per the M2 refinement); `JwtTokenService` issues short-lived access tokens (role/email/name
  claims) and longer-lived stateless refresh tokens (identity claim only, re-validated against
  the DB on every refresh so a role change or deactivation always takes effect); refresh token
  travels as an httpOnly/Secure cookie scoped to `/auth`, access token as a bearer header.
  `AddJwtAuthentication`'s `OnTokenValidated` hook re-checks `Editor.IsActive` on every request —
  verified end-to-end that deactivating an editor immediately 401s their still-unexpired access
  token, not just at natural expiry. `DbSeeder` idempotently bootstraps one `super_admin` from
  `Seed:SuperAdminEmail`/`Seed:SuperAdminPassword` (user-secrets) on startup — no password hash
  ever committed to source. `AuthController` (login/refresh/logout) and `EditorsController`
  (me/list/get/create/deactivate/reactivate/reset-password) added; role-gated endpoints verified
  against a live SuperAdmin + ZhEditor pair.
- **M4 (done)** — Entry CRUD against the reduced Stage-1 shape (`Entry → Homonym → Sense →
  Definition/Example/ZhSegment/KaSegment`, one implicit hidden `GramGrp` per homonym, `Sense`
  auto-numbered). `POST /entries` (headword-only minimum per spec §9), `PUT /entries/{id}/content`
  (**full-replace** save — deletes and re-creates all segments inside one explicit DB transaction
  so a concurrency conflict can't leave the old tree deleted without the new one committing;
  partial/diff-based save that preserves segment ids across saves is deferred to Stage 2, once
  Comments/XR actually need to target a specific segment), `GET /entries/{id}` (tree
  reconstruction from the flat `segments` table), `GET /entries` (5.1 main-list: `status_priority`
  asc + `updated_at_utc` desc, `new_entry` excluded — hits the M2 partial index directly),
  `GET /entries/search?q=` (5.2 dropdown: starts-with on `search_normalized_title` via
  `text_pattern_ops`, `title_length asc → status_priority asc → last_modified desc`, includes
  `new_entry`). Optimistic concurrency via the `xmin` shadow property, exposed to clients as
  `rowVersion`; a stale save returns 409. `POST`/`PUT` role-gated to
  SuperAdmin/ChiefEditor/ZhEditor/AssistantEditor (= "Edit new_entry content" in the 11.1
  permissions matrix — exact since every Stage-1 entry is perpetually `new_entry` until M5 adds
  status transitions). New pure `Domain/Rules`: `EntryStatusPriority` (status → sort priority)
  and `TitleNormalizer` (folds Chinese "，" to "," per 5.3), both unit-tested without a
  `DbContext`. Verified live end-to-end, including the 409-on-stale-save and
  KaEditor-cannot-create-entries cases.
- **M5 (done)** — `Domain/Rules/StatusTransitionRules.CanTransition(role, from, to)`, pure and
  exhaustively unit-tested (every role×status×status combination checked against an
  independently-written expected set — not just mirroring the implementation) against the
  resolved table above. `POST /entries/{id}/status` gated by it (no static role list — the
  actual check is inherently status-dependent). `MainAuthor` locks to `CreatedByEditorId` the
  first time an entry leaves `new_entry` — this alone already covers the "assistant-authored
  entry promoted by a supervisor keeps the assistant as main_author" case from Q8, with no
  special-casing needed, since the creator never changes. Every transition writes an
  `AuditLogEntry` (`AuditAction.StatusChanged`, old/new status as jsonb). Verified live
  end-to-end through the entire lifecycle of one entry: `new_entry → zh_review` (super_admin) →
  `ka_review` (zh_editor) → `ready` (ka_editor) → blocked publish attempt by zh_editor (403) →
  `published` (super_admin) → blocked backward move by zh_editor (403); plus same-status and
  unknown-status rejected with 400, and the entry correctly appearing in the main list only
  once it left `new_entry`. Known gap (not built): content-edit permissions
  (`PUT .../content`) are still a fixed role list, not status-aware (e.g. ka_editor should gain
  ka_review-scoped edit rights) — noted in the controller, deferred.
- **M6 (done)** — `IScoringService`/`ScoringService`: Main score to `MainAuthorEditorId`, once,
  the first time an entry leaves `new_entry`; Additional score to any other editor who edits
  content or changes status on an entry they don't author, once per entry (the `ScoreEntry`
  unique index from M2 is the real dedup guarantee, an in-request `AnyAsync` check is just the
  fast path); a dedicated KaEditor score — instead of Additional — when a ka_editor completes
  ka_review work (`ka_review → zh_review` or `ka_review → ready`). Hooked into
  `EntryService.SaveContentAsync` and `ChangeStatusAsync` so awards commit in the same
  transaction as the change that earned them. **Note**: the source spec explicitly flags the
  ka_editor score's "final score type" as unresolved ("საბოლოო ქულის ტიპი
  დასაზუსტებელია") — this implementation keeps it as the separate `KaEditor` bucket already
  baked into the M2 schema; tell the assistant if you'd rather fold it into Additional.
  `IAccountingService`/`AccountingService`: global per-editor score totals
  (`GET /accounting/editors`), entry counts by status (`GET /accounting/entries-status-totals`),
  and a personal accounting page with main/additional/ka-editor counts grouped by the *entry's
  current* status (`GET /accounting/editors/{id}`). Resolved Q5 privacy rule enforced in
  `AccountingController`: KA Editor and Assistant Editor may only view their own personal page
  and cannot see the global list; everyone else can see everyone's. Verified live end-to-end,
  including that the promoter (not the author) doesn't get Main score, that repeated
  edits/transitions by the same non-author editor only award Additional once, and both halves
  of the Q5 restriction (403 on someone else's page, 200 on your own).
- **M7 (done)** — `INotificationService.NotifyEntryChangedAsync`: on every content save or status
  change, notifies the entry's creator, its `MainAuthorEditorId` (once locked), and everyone
  with a prior `AuditLogEntry` against that entry — i.e. "everyone who ever worked on it," per
  spec §7 — excluding whoever just made the change (resolved Q4: no self-notifications). Reused
  the M2 `AuditLogEntry` table as the "who has touched this entry" ledger rather than adding a
  new contributor-tracking concept; `SaveContentAsync` now also logs `AuditAction.ContentEdited`
  (it previously only logged nothing — `ChangeStatusAsync` was the only audited path), which
  both completes the audit trail and is exactly what notification fan-out needs. Notifications
  link back to the entry (`entryId`/`entryLemma` in the response). Hooked into the same call
  sites as M6's scoring, staged on the shared `DbContext` so they commit atomically with the
  triggering change. Plain `DirectMessage` CRUD (`MessagesController`, separate from
  `NotificationsController` for cleaner routing — a small deviation from the originally sketched
  single-controller API surface): send, inbox (messages where you're sender or recipient), mark
  read. Ownership checked on mark-read for both notifications and messages (404, not 403, to
  avoid confirming another editor's data exists). Verified live: the actor never notifies
  themselves, past contributors do get notified, cross-user mark-read is rejected, and messaging
  round-trips correctly for both parties.
- **M8a (done)** — `Comment` CRUD (`CommentsController`): list/add per entry, edit/archive
  gated to the comment's own author or a Chief Editor/Super Admin override. Adding a comment
  awards Additional score and notifies past contributors exactly like a content edit (10.1:
  "commenting on another author's entry" is explicitly equivalent to editing it) — reuses
  `IScoringService.AwardForContentEditAsync`/`INotificationService.NotifyEntryChangedAsync`
  unchanged. Logs a new `AuditAction.CommentAdded` (required a migration to add the Postgres
  enum label). `ReferenceMaterial` get/save (`ReferenceMaterialController`, 1:1 with Entry,
  G1–G5 fields) — manually-authored rows leave `OriginalRawReferenceMaterial` empty; that field
  is populated only by the legacy-import tool (M8b). Both resources stay internal-only, never
  reachable from any public/entry-content response. Verified live: notification/scoring
  fan-out from a comment, non-owner non-privileged edit/archive rejected (403), privileged
  override works, reference material round-trips multi-script text (EN/ZH/RU) correctly.
- **M8b (not started)** — Legacy-import console tool: parses the old database's raw comment
  blobs (`&Author&` shift markers, `#...#` → Georgian editor notes) and reference-material
  blobs (`**`/`***`/`****`/`*****` delimiters, `//`/`///` → bullet markers), preserving the
  original raw text alongside the best-effort parsed structure. Deliberately deferred: needs
  real legacy data or DB access to build against meaningfully — product owner confirmed
  "CRUD first, import later."
- **Stage 2 (named only, detailed later)** — full IE/XR/STYLE/DOMAIN authoring, gramGrp/POS UI,
  TEI-XML export/import, media library, segment reorder, refined permission edge cases, fuzzy
  search.
