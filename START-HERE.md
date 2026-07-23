# START HERE — ERP-SYSTEM v1.0.9

> **One-click Docker install for Windows.** Login: `admin@alfajr.local` / `Demo1234`

---

## What's in v1.0.9

| Fix | What changed |
|-----|--------------|
| **CRITICAL — missing tables** | Added 14 missing JSON DataType files (Procurement, AR, Projects). DataTypeMigrator now creates all 51 tables on API startup. Dashboard errors `relation "purchase_orders" does not exist` are gone. |
| **No volume wipe needed** | The new tables are created on top of the existing volume. Your data is preserved. |
| **Updated SQL init script** | Fresh-volume installs now get all 51 tables in one shot. |

## Quick start (3 steps, ~3 minutes)

```powershell
# 1. Extract this ZIP to a NEW folder (e.g. F:\erpsystem7-23-2026\)
#    Make sure the path has NO spaces and NO Arabic characters.

# 2. Open PowerShell in the project root and run:
.\scripts\quickstart.ps1

# 3. Open http://localhost:3000 in your browser and log in:
#    Email:    admin@alfajr.local
#    Password: Demo1234
```

That's it. The script:
- Starts PostgreSQL, Redis, API, Frontend (4 containers)
- Waits for the DB to be ready
- Waits for the API to respond on `/health`
- Waits for the frontend on port 3000

## If you're updating from v1.0.8

You do NOT need to wipe the volume. Just re-run:

```powershell
.\scripts\quickstart.ps1
```

The `docker compose up -d --build` command will:
1. Rebuild the API image with the new JSON files
2. Restart the API container
3. DataTypeMigrator runs on startup → creates the 14 missing tables in place

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `relation "X" does not exist` in API logs | `docker compose restart api` (the DataTypeMigrator will create the missing table) |
| Login returns 500 | `docker logs erp-api --tail 50` and check what relation is missing |
| `admin@alfajr.local` doesn't work | Re-run `.\scripts\quickstart.ps1 -Reset` to wipe the volume and reseed |
| Want to see seed progress | `.\scripts\quickstart.ps1 -Logs` |

## Other commands

```powershell
.\scripts\quickstart.ps1 -Down     # stop everything
.\scripts\quickstart.ps1 -Status   # show container status
.\scripts\quickstart.ps1 -Logs     # tail logs from all containers
.\scripts\quickstart.ps1 -Reset    # DESTRUCTIVE: stop + delete all data
.\scripts\quickstart.ps1 -Help     # show this help
```

## Default credentials

| Email | Password |
|-------|----------|
| `admin@alfajr.local` | `Demo1234` |

These are created automatically by `AdminUserSeederHostedService` on first start.

## Architecture

- **Backend**: ASP.NET Core 9 + FluentMigrator + Dapper + Npgsql
- **Frontend**: Next.js 14 + TypeScript + Tailwind + shadcn/ui
- **Database**: PostgreSQL 15
- **Cache**: Redis 7
- **Schema**: JSON-driven via `src/backend/Host/data-types/*.json` (51 tables)
- **Migrations**: `src/backend/Shared/Migrations/*.cs` (NoOp + version tracking)
- **Tables Created By**: `DataTypeMigrator` on API startup (idempotent)
