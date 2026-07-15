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

- **Auth**: JWT bearer via ASP.NET Core Identity (`Editor : IdentityUser<int>`), short-lived
  access token + refresh token in an httpOnly cookie. Role kept as a typed enum column (drives
  the status-transition table directly in C#) but also emitted as a claim for
  `[Authorize(Roles=)]`.
- **Audit logging**: a generic `SaveChangesInterceptor` (catches all tracked changes) plus
  explicit semantic calls from services for meaningful events (StatusChanged, RoleChanged).
- **Side effects** (scoring/notifications/audit on status change): plain **service-layer
  orchestration** in one DB transaction — no MediatR/domain-event bus, for a 1-developer team.
- **Local dev**: Postgres runs locally; the connection string lives in `dotnet user-secrets`
  for `Chiniseapp.Api`, never in `appsettings.json`.

## Stage 1 API surface (built across M3–M8)

`AuthController`, `EditorsController`, `EntriesController` (list/search/get/create/save,
`POST /entries/{id}/status` gated by the role×status transition table), `CommentsController`,
`ReferenceMaterialController`, `AccountingController`, `NotificationsController`,
`VocabController`, `AuditController`.

## Phased roadmap

- **M1 (done)** — solution scaffold, EF Core+Npgsql wiring, empty `ChiniseDbContext`,
  `/health/db` connectivity smoke check.
- **M2** — Postgres enums, `ControlledVocabulary`, `Editor`/Identity, `Entry`, `Segment` +
  configurations/indexes; first migration.
- **M3** — Auth (Identity + JWT), role claim, seed one `super_admin`.
- **M4** — Entry CRUD + reduced-shape segment save (implicit Homonym/GramGrp), editor-list +
  search endpoints, optimistic concurrency.
- **M5** — Status workflow (`StatusTransitionRules`), MainAuthor lock logic, audit entries.
- **M6** — Scoring + accounting endpoints.
- **M7** — Notifications + direct messaging.
- **M8** — Comments + Reference Material endpoints + legacy-import console tool (parses
  `&Author&`/`#...#` comments and `**`/`***`/`****`/`*****` reference-material segments from
  the old database).
- **Stage 2 (named only, detailed later)** — full IE/XR/STYLE/DOMAIN authoring, gramGrp/POS UI,
  TEI-XML export/import, media library, segment reorder, refined permission edge cases, fuzzy
  search.
