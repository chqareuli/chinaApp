# Chinese–Georgian Dictionary — Backend

Backend for the rebuild of the chinese.ge editorial platform. The Angular admin panel and
public site are built separately and are not part of this repo.

Full architecture rationale and phased roadmap: see
`docs/backend-plan.md` (mirrors the plan agreed with the product owner).

## Solution layout

```
chiniseapp.sln
 src/Chiniseapp.Domain/          net8.0, no external deps — entities, enums, pure rules
 src/Chiniseapp.Application/     net8.0, refs Domain — services, interfaces, DTOs
 src/Chiniseapp.Infrastructure/  net8.0, refs Domain+Application — EF Core, Npgsql, repositories
 src/Chiniseapp.Api/             net8.0 ASP.NET Core — composition root (Program.cs, controllers)
 tests/Chiniseapp.Tests/         xUnit
```

Reference direction: `Api → Application → Domain`; `Infrastructure → Application + Domain`.
`Api` depends on `Infrastructure` only for DI registration (`AddInfrastructure(...)`), never for
business logic directly.

## Local setup

1. PostgreSQL running locally (database `ChinesDbGeo`).
2. Set the connection string via user-secrets (not `appsettings.json` — that file intentionally
   ships with an empty `ConnectionStrings:DefaultConnection`):
   ```
   cd src/Chiniseapp.Api
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=ChinesDbGeo;Username=postgres;Password=..."
   ```
3. `dotnet build` from the repo root.
4. `dotnet run --project src/Chiniseapp.Api` → Swagger at `/swagger`,
   `GET /health/db` proves the Api → Infrastructure → Postgres wiring is live.

## Roadmap

**Stage 1** — preserve the legacy program's statuses/roles/workflow, but store entries as
genuine structured data (not raw text) so it's a real subset of Stage 2, not a rewrite target.

- **M1 (done)** — solution scaffold, EF Core+Npgsql wiring, empty `ChiniseDbContext`, `/health/db`.
- **M2** — Postgres enums, `ControlledVocabulary`, `Editor`/Identity, `Entry`, `Segment` +
  configurations/indexes; first migration.
- **M3** — Auth (Identity + JWT), role claim, seed one `super_admin`.
- **M4** — Entry CRUD + reduced-shape segment save (Entry → Homonym → Sense →
  Definition/Example/ZhSegment/KaSegment, implicit hidden GramGrp), editor-list + search
  endpoints, optimistic concurrency.
- **M5** — Status workflow (`StatusTransitionRules`), MainAuthor lock logic, audit entries.
- **M6** — Scoring + accounting endpoints.
- **M7** — Notifications + direct messaging.
- **M8** — Comments + Reference Material endpoints + legacy-import console tool (parses
  `&Author&` / `#...#` comment markers and `**`/`***`/`****`/`*****` reference-material
  segments from the old database).

**Stage 2** (named only, detailed later) — full IE/XR/STYLE/DOMAIN authoring, gramGrp/POS UI,
TEI-XML export/import, media library, segment reorder, refined permission edge cases, fuzzy
search.
