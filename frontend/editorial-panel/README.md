# Chinese.ge Editorial Panel

Angular 22 frontend for the Chinese–Georgian dictionary editorial platform.
This is an independent Angular workspace that consumes the ASP.NET Core API in
the repository root.

## Local development

Prerequisites:

- Node.js 20.19+ (Node 24 is currently used locally)
- the backend API running at `https://localhost:7010`
- the backend database and user-secrets configured as described in the root README

Run the UI:

```powershell
cd frontend/editorial-panel
npm install
npm start
```

Open `http://localhost:4200`. The Angular dev server proxies `/api/*` to the
backend and rewrites the secure refresh-token cookie path for local development.

## Commands

```powershell
npm run build
npm test -- --watch=false
```

For UI-only development and browser QA without changing the real database, run these in two terminals:

```powershell
npm run mock-api
npm run start:qa
```

## Implemented

- standalone, strict Angular application with lazy routes;
- real login/refresh/logout API integration;
- bearer-token interceptor and protected routes;
- responsive editorial shell and Georgian navigation;
- status labels and the gray/red/blue/green/black color system;
- dashboard statistics, paged entry list, search dropdown, loading/error states;
- new-entry creation against the API;
- structured homonym/sense/example editor with optimistic-concurrency UX;
- backend-driven content permissions and status-transition actions;
- comments and G1-G5 reference-material editing;
- notification center and conversation-style private messages;
- automatic Chinese-token links to matching entries;
- SuperAdmin user/role/block/password-reset management;
- personal/global accounting and status totals;
- isolated mock API for safe browser QA.

See `../../docs/frontend-plan.md` for the delivery phases and backend prerequisites.
