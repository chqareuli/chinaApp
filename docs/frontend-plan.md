# Angular Editorial Panel — Delivery Plan

## Scope decision

- The legacy database import is deferred until the existing database can be tested with real data.
- The Angular UI is developed on the dedicated `codex/angular-ui` branch as an independent project
  under `frontend/editorial-panel`.
- The product owner's latest Georgian requirements are the acceptance baseline where older PDFs
  conflict with them. Before status-editing UI is enabled, the final role × status matrix must be
  encoded in the backend and integration-tested.

## UI architecture

- Angular 22, standalone components, strict TypeScript, SCSS, lazy routes.
- Functional HTTP interceptor, JWT access token, secure refresh cookie.
- API base URL provided through DI (`/api` locally) so production deployment can use a reverse proxy.
- Feature-first layout: auth, dashboard, entries, notifications, messages, accounting, users/media.
- No fake fallback data in production code; API failures are surfaced explicitly.

## Delivery phases

### UI-1 — Foundation and first vertical slice (done)

- [x] Angular workspace and branch isolation
- [x] authentication client, token refresh and route guards
- [x] responsive admin shell
- [x] status label/color system
- [x] main entry list, pagination and title-prefix search
- [x] new-entry creation
- [x] read-only structured entry view
- [x] browser QA against an isolated API contract (real DB connection secret was unavailable)

### UI-2 — Full entry workflow

- [x] structured editor for homonyms, senses, definitions and examples
- [x] status-aware editing controls and transition actions
- [x] optimistic-concurrency conflict UX
- [x] comments and G1–G5 reference-material tabs
- [x] publish validation summary and blocking errors in the UI
- [x] change history panel backed by the entry audit timeline

### UI-3 — Collaboration

- [x] notification center with unread state and direct entry navigation
- [x] conversation-based private messages
- [x] Chinese-token detection and entry linkification
- [ ] notification preferences when the backend contract exists

### UI-4 — Administration and accounting

- [x] user list, create, block/reactivate, password reset and role assignment
- [x] role-aware accounting pages and status totals
- [x] personal profile and password update

### UI-5 — Media and extended editorial model

- [x] validated entry-image upload, authenticated preview and removal
- [ ] shared media library, video support and exact segment attachment
- POS, gramGrp, IE, XR, STYLE, DOMAIN, abbreviation and language controls
- segment reorder and richer preview
- TEI XML / JSON export UI after backend exporters exist

### UI-6 — Release hardening

- accessibility and keyboard-navigation audit
- responsive/browser matrix
- API integration and end-to-end tests for every role
- production reverse proxy, environment configuration and deployment pipeline

## Backend prerequisites before UI-2 is considered complete

1. Status-aware content permissions: Assistant only `NewEntry`; KA Editor only `KaReview`;
   Editor may edit published content but cannot publish or move published entries backward.
2. Final ZH transition scope based on the latest requirements.
3. Publish validation that prevents title-only or structurally invalid entries from publishing.
4. SuperAdmin-only user/role management, including an explicit role-change endpoint and audit rows.
5. Entry `LastEditor`/`UpdatedAt` updates for comment and reference-material changes.
6. Stable API status/role codes and one documented casing convention.
7. Integration tests for role × status × action, authentication refresh, list/search, notifications,
   comments and reference material.

## Definition of done

The editorial application is complete when every role can perform only its allowed actions, all
five status colors and transitions are visible and tested, entry editing is structurally validated,
collaboration/admin/media modules are functional, and the production UI passes build, unit,
integration, accessibility and browser checks against the real database.
