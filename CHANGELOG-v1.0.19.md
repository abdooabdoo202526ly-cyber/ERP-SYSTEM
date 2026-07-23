# v1.0.19 — FIX 401 Unauthorized (auth header on raw fetch)

## The bug

22 frontend pages use raw `fetch('/api/...')` instead of the `api` axios instance.
The raw `fetch` calls did NOT include the `Authorization: Bearer <token>` header,
so the backend returned 401 Unauthorized even when the user was logged in.

Symptoms:
- Sidebar shows "AlFajr Administrator admin@alfajr.local" (logged in)
- POST `/api/finance/accounts` returns 401
- Login works (it used `api.ts` instance)
- Any form submission fails

## The fix

Added a **monkey-patch** on `window.fetch` in `src/frontend/lib/api.ts` that:
1. Detects any `/api/*` URL
2. Reads `accessToken` from `localStorage`
3. Attaches `Authorization: Bearer <token>` header if not already present
4. Runs once (idempotent — guarded with `__fetchPatched` flag)

This fixes all 22 affected files without touching their code. The patch is
safe because:
- It only intercepts URLs starting with `/api/`
- It doesn't override headers that are explicitly set
- It uses `localStorage` which is already used by `api.ts` axios instance

## What was verified

- Next.js build: 0 errors, 65/65 pages generated
- All 22 hardcoded `fetch('/api/...')` calls will now auto-attach the token
- Login flow: still uses `api.ts` (no change)

## How to use

Same as before. Extract v1.0.19 in `F:\erpsystem7-23-2026\ERP-SYSTEM\`,
then run:

```powershell
.\scripts\NUKE-AND-INSTALL.ps1
```

## Files changed

- `src/frontend/lib/api.ts` — added fetch interceptor (monkey-patch)
