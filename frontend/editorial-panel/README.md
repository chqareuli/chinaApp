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

## Implemented in the first UI increment

- standalone, strict Angular application with lazy routes;
- real login/refresh/logout API integration;
- bearer-token interceptor and protected routes;
- responsive editorial shell and Georgian navigation;
- status labels and the gray/red/blue/green/black color system;
- dashboard statistics, paged entry list, search dropdown, loading/error states;
- new-entry creation against the API;
- read-only structured entry detail view.

See `../../docs/frontend-plan.md` for the delivery phases and backend prerequisites.
