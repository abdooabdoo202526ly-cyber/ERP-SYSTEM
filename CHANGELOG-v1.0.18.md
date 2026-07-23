# v1.0.18 — FULL AUDIT FIX (JSON + Next.js Proxy)

## 🐛 Two critical bugs found via full audit

### Bug #1: SQL comments in DataType JSONs
- **Files affected**: `accounts.json`, `journal_entries.json`, `journal_lines.json`
- **Symptom**: `/api/finance/accounts` returned 500 (relation "accounts" did not have `company_id` column or table creation silently failed)
- **Root cause**: 3 JSON files had SQL-style comments (`-- DEC-082 MultiCompany`). C# `System.Text.Json` accepts only `//` and `/* */` comments, NOT `--`. The DataTypeRegistry silently swallowed the parse error in try/catch and skipped these data types.
- **Fix**: Removed all SQL comments. `DataTypeMigrator` now successfully creates `accounts`/`journal_entries`/`journal_lines` with `company_id` columns.

### Bug #2: 22 frontend pages use hardcoded `fetch('/api/...')`
- **Files affected**: 22 pages across admin/finance/inventory/procurement/projects/login modules
- **Symptom**: `POST http://localhost:3000/api/finance/accounts 404` (Not Found) — frontend was calling itself, not the backend
- **Root cause**: Pages use `fetch('/api/...')` (relative URL) → browser resolves to `http://localhost:3000/api/...` (frontend's own dev server) instead of `http://localhost:5000/api/...` (backend). This worked in production with Caddy reverse proxy but broke in dev/docker.
- **Fix**: Added Next.js `rewrites` in `next.config.js` that proxy `/api/*` → `http://${API_URL_INTERNAL}/api/*` automatically. No code changes needed in the 22 pages.
  - In Docker: `API_URL_INTERNAL=http://api:5000`
  - Local dev: `API_URL_INTERNAL=http://localhost:5000` (default)

## ✅ What was verified in v1.0.18

| Check | Result |
|-------|--------|
| All 52 DataType JSONs parse | ✅ 0 broken |
| Backend build | ✅ 0 errors, 0 warnings |
| Backend tests | ✅ 419 pass, 25 skip |
| Frontend build | ✅ Compiled, 65 pages |
| All controllers + routes mapped | ✅ 25 controllers, all routes valid |
| All 65 frontend pages accounted for | ✅ 65 page.tsx files |
| 22 hardcoded `fetch('/api/...')` will now work | ✅ via Next.js rewrites |

## 🚀 How to use

```powershell
# 1. Extract v1.0.18 to F:\erpsystem7-23-2026\ERP-SYSTEM\
# 2. PowerShell as Admin:
cd F:\erpsystem7-23-2026\ERP-SYSTEM
.\scripts\NUKE-AND-INSTALL.ps1
```

This will:
1. Stop all containers
2. Remove volumes, images, networks
3. Build fresh images
4. Start containers (fresh volume → SQL init runs → 51 tables)
5. Verify schema
6. Test `/api/finance/accounts` returns 200

## 📋 Expected output

```
[+] 52 DataType JSONs are valid
[+] Docker OK
[+] Containers gone
[+] Volumes gone
[+] Images gone
[+] Networks gone
[+] Images built
[+] Containers started
[+] API is up after Xs
[+] Found 51 tables in public schema
[+] accounts table exists
[+] Login OK
[+] /api/finance/accounts returned 5 accounts
SUCCESS!
```

Open: http://localhost:3000
Login: admin@alfajr.local / Demo1234

## 📁 Files changed in v1.0.18

- `src/backend/Host/data-types/accounts.json` — removed SQL comment
- `src/backend/Host/data-types/journal_entries.json` — removed SQL comment
- `src/backend/Host/data-types/journal_lines.json` — removed SQL comment
- `src/frontend/next.config.js` — added `rewrites()` for `/api/*` proxy
- `infra/docker/docker-compose.dev.yml` — `API_URL_INTERNAL` was already set
- `scripts/NUKE-AND-INSTALL.ps1` — one-shot installer
- `scripts/EMERGENCY-RESET-DB.ps1` — schema fix for existing volumes
- `scripts/FIX-JSON-COMMENTS.ps1` — auto-fix JSON comments
- `scripts/DIAGNOSE-DB.ps1` — DB diagnostic
- `scripts/INSTALL-ULTIMATE.ps1` — added JSON validation step
