# CHANGELOG — ERP-SYSTEM v1.0.9

**Release Date:** 2026-07-23
**Type:** Critical Fix
**Recommended action:** Replace your project folder with this ZIP, then run `.\scripts\quickstart.ps1`.

---

## v1.0.9 — 2026-07-23 (CRITICAL)

### Fixed
- **CRITICAL — `relation "purchase_orders" does not exist`** on dashboard.
  - Root cause: `DataTypeMigrator` (the JSON-driven schema builder) was missing 14 JSON files in `src/backend/Host/data-types/`. All migrations are `NoOp` since DEC-079, so the entire schema comes from JSON. The 14 missing entities had no JSON → their tables were never created.
  - Fix: Added 14 JSON DataType files. The migrator is additive and idempotent, so it creates only the missing tables on next API startup. **No volume wipe needed** — existing data is preserved.
- **`neondb` → `erp_system` typo** in `scripts/quickstart.ps1:68` and `:92`. Cosmetic but caused misleading output when starting in non-Docker mode.

### Added
- `src/backend/Host/data-types/purchase_orders.json`
- `src/backend/Host/data-types/purchase_order_lines.json`
- `src/backend/Host/data-types/goods_receipts.json`
- `src/backend/Host/data-types/goods_receipt_lines.json`
- `src/backend/Host/data-types/vendor_bills.json`
- `src/backend/Host/data-types/vendor_bill_lines.json`
- `src/backend/Host/data-types/sales_invoices.json`
- `src/backend/Host/data-types/sales_invoice_lines.json`
- `src/backend/Host/data-types/receipts.json`
- `src/backend/Host/data-types/receipt_allocations.json`
- `src/backend/Host/data-types/project_budgets.json`
- `src/backend/Host/data-types/project_tasks.json`
- `src/backend/Host/data-types/resources.json`
- `src/backend/Host/data-types/resource_assignments.json`
- `START-HERE.md` — quick start guide for Windows
- `CHANGELOG-v1.0.9.md` — this file
- Extended `infra/docker/init-scripts/02-create-tables.sql` from 37 → 51 tables (for fresh-volume installs)

### Verified
- All 14 new JSON files validated with `python3 -c "import json; json.load(open(f))"` — all OK
- SQL init script: 51 `CREATE TABLE IF NOT EXISTS` statements, all idempotent
- `DataTypeMigrator` is enabled in `appsettings.json` (`Database.JsonMigrationEnabled: true`) — runs on every API startup
- Backend Dockerfile: `COPY . .` includes `data-types/` folder in the published image

### Known limitations
- The `DataTypeMigrator` is **additive only** — it never drops or alters columns. If you change a JSON field type, you need a manual migration.
- Foreign keys are added idempotently but only on the first time the column is created. Adding a new FK to an existing column requires a manual migration.
- The SQL init script only runs on **fresh volumes** (first time PostgreSQL is initialized). If you wipe the volume (`docker compose down -v`), the script will recreate all 51 tables. Otherwise, the DataTypeMigrator handles the diff.

---

## Upgrade path (from v1.0.8)

1. **Save your data** (just in case): no need to back up the volume, but if you have custom companies/accounts, export them as JSON via `/api/companies` and `/api/finance/accounts` GET endpoints.
2. **Replace the project folder**: extract `ERP-SYSTEM-v1.0.9.zip` to a new path (e.g. `F:\erpsystem7-23-2026\`). Don't overwrite the old folder in place — keep v1.0.8 as a rollback.
3. **Run the quickstart**:
   ```powershell
   cd F:\erpsystem7-23-2026\ERP-SYSTEM
   .\scripts\quickstart.ps1
   ```
4. **Verify**:
   - `docker logs erp-api --tail 50` — look for `[DataTypeMigrator] Created table purchase_orders` (and the other 13)
   - Open http://localhost:3000 → login with `admin@alfajr.local` / `Demo1234`
   - Navigate to Dashboard → Procurement → no more 500s

### If you want a clean slate instead

```powershell
.\scripts\quickstart.ps1 -Reset
```

This wipes the postgres volume and reseeds. Use this if v1.0.9 doesn't fix the issue or you want a clean test.

---

## Default credentials (unchanged)

| Email | Password |
|-------|----------|
| `admin@alfajr.local` | `Demo1234` |

Created automatically by `AdminUserSeederHostedService` on first start.

---

## Architecture (unchanged)

- **Backend**: ASP.NET Core 9 + FluentMigrator + Dapper + Npgsql
- **Frontend**: Next.js 14 + TypeScript + Tailwind + shadcn/ui
- **Database**: PostgreSQL 15
- **Cache**: Redis 7
- **Schema source**: `src/backend/Host/data-types/*.json` (51 tables now)
- **Migrations**: `src/backend/Shared/Migrations/*.cs` (all NoOp + version tracking)
- **Tables Created By**: `DataTypeMigrator` on API startup (idempotent, additive)

---

## Files modified in v1.0.9

```
A  src/backend/Host/data-types/goods_receipt_lines.json
A  src/backend/Host/data-types/goods_receipts.json
A  src/backend/Host/data-types/project_budgets.json
A  src/backend/Host/data-types/project_tasks.json
A  src/backend/Host/data-types/purchase_order_lines.json
A  src/backend/Host/data-types/purchase_orders.json
A  src/backend/Host/data-types/receipt_allocations.json
A  src/backend/Host/data-types/receipts.json
A  src/backend/Host/data-types/resource_assignments.json
A  src/backend/Host/data-types/resources.json
A  src/backend/Host/data-types/sales_invoice_lines.json
A  src/backend/Host/data-types/sales_invoices.json
A  src/backend/Host/data-types/vendor_bill_lines.json
A  src/backend/Host/data-types/vendor_bills.json
M  infra/docker/init-scripts/02-create-tables.sql     (37 → 51 tables)
M  scripts/quickstart.ps1                              (neondb → erp_system)
A  START-HERE.md
A  CHANGELOG-v1.0.9.md
```

A = Added, M = Modified
