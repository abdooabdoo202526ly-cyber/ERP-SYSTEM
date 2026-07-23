# v1.0.17 — CRITICAL JSON FIX + NUKE Installer

## 🐛 THE BUG (root cause of persistent 500 errors)

**3 DataType JSON files contained SQL-style comments (`--`) which the C# `System.Text.Json` parser CANNOT handle.** Only `//` (JS-style) and `/* */` (CSS-style) are valid JSON comments. `--` is not. The parser would fail silently (try/catch in `DataTypeRegistry.LoadFromDirectory` adds to errors but skips the DataType), meaning:

- `accounts.json` → not registered → `accounts` table not created from JSON; only from SQL init
- `journal_entries.json` → not registered
- `journal_lines.json` → not registered

This explains why:
- POST /api/ar/customers worked (the table was created with proper schema)
- GET /api/finance/accounts failed (the `accounts` table was missing `company_id` column, or didn't exist in some volumes)

The bug was **invisible** because:
- DataTypeRegistry swallows per-file errors
- DataTypeMigrator doesn't validate JSON before iterating
- INSTALL-ULTIMATE.ps1 didn't validate JSON files

## ✅ THE FIX

### 1. Removed all SQL comments from JSON files
- `src/backend/Host/data-types/accounts.json`
- `src/backend/Host/data-types/journal_entries.json`
- `src/backend/Host/data-types/journal_lines.json`

### 2. New script: `scripts/FIX-JSON-COMMENTS.ps1`
- Scans all `data-types/*.json` and strips any `-- comment` patterns
- Idempotent, safe to re-run

### 3. New script: `scripts/NUKE-AND-INSTALL.ps1`
- One-shot definitive installer: stops containers, removes volumes + images, rebuilds, starts, verifies
- Calls EMERGENCY-RESET-DB if schema incomplete
- Verifies /api/finance/accounts returns 200 (the original failing endpoint)

### 4. New script: `scripts/EMERGENCY-RESET-DB.ps1`
- 10 idempotent DO blocks that:
  - Make `accounts`/`customers`/`sales_invoices`/`receipts`.company_id nullable
  - Add `company_id` column to accounts if missing
  - Convert `audit_log.id` to bigserial + ensure sequence
  - Add `updated_at` to `units_of_measure`
  - Add `tax_id`/`phone`/`email`/`address` to `companies`
  - Drop+recreate FKs on company_id with ON DELETE SET NULL

### 5. Enhanced `scripts/INSTALL-ULTIMATE.ps1`
- Added JSON validation step before docker build
- Detects `-- ` comments inside JSON values and aborts with clear error

### 6. New script: `scripts/DIAGNOSE-DB.ps1`
- 10-point DB diagnostic that prints table count, critical tables, schema, recent errors
- For ad-hoc debugging

## 🚀 How to use

### Option A: Nuke and reinstall (fastest, recommended)
```powershell
cd F:\erpsystem7-23-2026\ERP-SYSTEM
.\scripts\NUKE-AND-INSTALL.ps1
```

### Option B: Diagnose first
```powershell
.\scripts\DIAGNOSE-DB.ps1
# Send output to Mavis
```

### Option C: Manual fix existing volume
```powershell
.\scripts\FIX-JSON-COMMENTS.ps1
.\scripts\EMERGENCY-RESET-DB.ps1
docker restart erp-api
```

## 📊 What to expect

After successful install:
```
[+] Found 51 tables in public schema
[+] accounts table exists
[+] Login OK
[+] /api/finance/accounts returned 5 accounts
```

Open: http://localhost:3000  
Login: admin@alfajr.local / Demo1234
