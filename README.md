# Chinese–Georgian Dictionary — Backend

Backend for the rebuild of the chinese.ge editorial platform. The Angular admin panel and
public site are built separately and are not part of this repo.

Full architecture rationale and phased roadmap: see
`docs/backend-plan.md` (mirrors the plan agreed with the product owner).
Current milestone / what's next: see `docs/PROGRESS.md`.

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
   Also set JWT signing config and the bootstrap super_admin credentials (never committed):
   ```
   dotnet user-secrets set "Jwt:Issuer" "chiniseapp-api"
   dotnet user-secrets set "Jwt:Audience" "chiniseapp-clients"
   dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"   # >= 256 bits
   dotnet user-secrets set "Jwt:AccessTokenMinutes" "30"
   dotnet user-secrets set "Jwt:RefreshTokenDays" "14"
   dotnet user-secrets set "Seed:SuperAdminEmail" "admin@chinese.ge"
   dotnet user-secrets set "Seed:SuperAdminPassword" "<pick one>"
   ```
3. `dotnet build` from the repo root.
4. Apply migrations (first run, and after pulling new ones):
   ```
   dotnet tool install --global dotnet-ef   # once
   dotnet ef database update \
     --project src/Chiniseapp.Infrastructure/Chiniseapp.Infrastructure.csproj \
     --startup-project src/Chiniseapp.Api/Chiniseapp.Api.csproj
   ```
5. `dotnet run --project src/Chiniseapp.Api` → Swagger at `/swagger`,
   `GET /health/db` proves the Api → Infrastructure → Postgres wiring is live. On first run this
   also bootstraps the `super_admin` account from the `Seed:*` secrets above.
6. `POST /auth/login` with `{"email": "...", "password": "..."}` returns an access token
   (use as `Authorization: Bearer <token>`) and sets an httpOnly refresh-token cookie — requires
   HTTPS locally (`dotnet dev-certs https --trust` once), since the cookie is `Secure`.

To add a new migration after changing entities/configurations:
```
dotnet ef migrations add <Name> \
  --project src/Chiniseapp.Infrastructure/Chiniseapp.Infrastructure.csproj \
  --startup-project src/Chiniseapp.Api/Chiniseapp.Api.csproj \
  --output-dir Persistence/Migrations
```

## Roadmap

**Stage 1** — preserve the legacy program's statuses/roles/workflow, but store entries as
genuine structured data (not raw text) so it's a real subset of Stage 2, not a rewrite target.

- **M1 (done)** — solution scaffold, EF Core+Npgsql wiring, empty `ChiniseDbContext`, `/health/db`.
- **M2 (done)** — Postgres enums, `ControlledVocabulary`, `Editor`, `Entry`, `Segment` and the
  rest of the Stage-1 entity set + configurations/indexes; first migration applied.
- **M3 (done)** — JWT auth (login/refresh/logout), role claims, live deactivation check,
  bootstrap `super_admin` seeding, basic editor management endpoints.
- **M4 (done)** — Entry CRUD (`POST /entries`, `PUT /entries/{id}/content`, `GET /entries/{id}`,
  `GET /entries`, `GET /entries/search`) against the reduced Stage-1 segment shape, full-replace
  content save, optimistic concurrency (`rowVersion` / `xmin`), editor-list and search-dropdown
  sort algorithms.
- **M5 (done)** — Status workflow (`POST /entries/{id}/status`), `StatusTransitionRules`
  (exhaustively unit-tested), `MainAuthor` lock, `AuditLogEntry` on every transition.
- **M6** — Scoring + accounting endpoints.
- **M7** — Notifications + direct messaging.
- **M8** — Comments + Reference Material endpoints + legacy-import console tool (parses
  `&Author&` / `#...#` comment markers and `**`/`***`/`****`/`*****` reference-material
  segments from the old database).

**Stage 2** (named only, detailed later) — full IE/XR/STYLE/DOMAIN authoring, gramGrp/POS UI,
TEI-XML export/import, media library, segment reorder, refined permission edge cases, fuzzy
search.
