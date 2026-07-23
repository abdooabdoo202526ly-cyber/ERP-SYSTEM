# =============================================================================
# ERP-SYSTEM v1.0.17 - EMERGENCY RESET DB
# =============================================================================
# يصلّح الـ schema الموجود بدون حذف volume.
# لو الـ volume قديماً وفيه tables ناقصة columns، ده يصلّحها.
#
# Usage:
#   powershell -File scripts\EMERGENCY-RESET-DB.ps1
# =============================================================================

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function W($c, $m) { Write-Host $m -ForegroundColor $c }

# Verify postgres is up
$pgRunning = docker ps --format "{{.Names}}" 2>$null | Select-String -Pattern "^erp-postgres$"
if (-not $pgRunning) {
    W Red "[X] erp-postgres is not running"
    exit 1
}

W Cyan "============================================================"
W Cyan "  ERP-SYSTEM v1.0.17 - EMERGENCY DB RESET"
W Cyan "============================================================"
W Cyan ""

$fixes = @(
    # 1. accounts: ensure company_id column exists + nullable
    @"
DO `$`$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'accounts' AND column_name = 'company_id') THEN
        ALTER TABLE accounts ADD COLUMN company_id uuid;
    END IF;
    ALTER TABLE accounts ALTER COLUMN company_id DROP NOT NULL;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'accounts.company_id: %', SQLERRM;
END
`$`$;
"@,

    # 2. customers: ensure company_id nullable
    @"
DO `$`$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'customers' AND column_name = 'company_id') THEN
        ALTER TABLE customers ALTER COLUMN company_id DROP NOT NULL;
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'customers.company_id: %', SQLERRM;
END
`$`$;
"@,

    # 3. sales_invoices: ensure company_id nullable
    @"
DO `$`$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'sales_invoices' AND column_name = 'company_id') THEN
        ALTER TABLE sales_invoices ALTER COLUMN company_id DROP NOT NULL;
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'sales_invoices.company_id: %', SQLERRM;
END
`$`$;
"@,

    # 4. receipts: ensure company_id nullable
    @"
DO `$`$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'receipts' AND column_name = 'company_id') THEN
        ALTER TABLE receipts ALTER COLUMN company_id DROP NOT NULL;
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'receipts.company_id: %', SQLERRM;
END
`$`$;
"@,

    # 5. audit_log: ensure id is bigserial
    @"
DO `$`$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'audit_log' AND column_name = 'id' AND data_type <> 'bigint') THEN
        -- Convert to bigint (assumes no FK dependencies)
        CREATE SEQUENCE IF NOT EXISTS audit_log_id_seq;
        ALTER TABLE audit_log ALTER COLUMN id SET DEFAULT nextval('audit_log_id_seq');
        ALTER TABLE audit_log ALTER COLUMN id TYPE bigint;
        UPDATE audit_log SET id = nextval('audit_log_id_seq') WHERE id IS NULL;
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'audit_log.id: %', SQLERRM;
END
`$`$;
"@,

    # 6. units_of_measure: ensure updated_at column
    @"
DO `$`$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'units_of_measure' AND column_name = 'updated_at') THEN
        ALTER TABLE units_of_measure ADD COLUMN updated_at timestamptz NOT NULL DEFAULT now();
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'units_of_measure.updated_at: %', SQLERRM;
END
`$`$;
"@,

    # 7. companies: ensure contact fields
    @"
DO `$`$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'companies' AND column_name = 'tax_id') THEN
        ALTER TABLE companies ADD COLUMN tax_id varchar(100);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'companies' AND column_name = 'phone') THEN
        ALTER TABLE companies ADD COLUMN phone varchar(50);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'companies' AND column_name = 'email') THEN
        ALTER TABLE companies ADD COLUMN email varchar(200);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'companies' AND column_name = 'address') THEN
        ALTER TABLE companies ADD COLUMN address text;
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'companies.*: %', SQLERRM;
END
`$`$;
"@,

    # 8. audit_log_id_seq sequence
    @"
DO `$`$
BEGIN
    CREATE SEQUENCE IF NOT EXISTS audit_log_id_seq;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'audit_log_id_seq: %', SQLERRM;
END
`$`$;
"@,

    # 9. Drop+recreate FKs on nullable company_id columns (ON DELETE SET NULL)
    @"
DO `$`$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT c.conname, c.conrelid::regclass AS tbl
        FROM pg_constraint c
        JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY(c.conkey)
        WHERE c.contype = 'f' AND a.attname = 'company_id' AND c.confdeltype <> 'a'  -- not NO ACTION
    LOOP
        EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname);
    END LOOP;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'FK cleanup: %', SQLERRM;
END
`$`$;
"@,

    # 10. Re-add FKs for the 3 tables with nullable company_id
    @"
DO `$`$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_customers_company_id') THEN
        ALTER TABLE customers ADD CONSTRAINT fk_customers_company_id FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE SET NULL;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_sales_invoices_company_id') THEN
        ALTER TABLE sales_invoices ADD CONSTRAINT fk_sales_invoices_company_id FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE SET NULL;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_receipts_company_id') THEN
        ALTER TABLE receipts ADD CONSTRAINT fk_receipts_company_id FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE SET NULL;
    END IF;
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE 'FK re-add: %', SQLERRM;
END
`$`$;
"@
)

$idx = 0
foreach ($sql in $fixes) {
    $idx++
    W Yellow "[*] Fix #$idx..."
    $r = docker exec erp-postgres psql -U erp_user -d erp_system -c $sql 2>&1
    if ($LASTEXITCODE -eq 0) {
        W Green "    [+] OK"
    } else {
        W Red "    [X] FAILED: $r"
    }
}

W Cyan ""
W Cyan "============================================================"
W Cyan "  EMERGENCY RESET COMPLETE"
W Cyan "============================================================"
W Cyan ""
W Yellow "Restarting API to apply changes..."
docker restart erp-api | Out-Null
W Green "[+] API restarted"
W Cyan ""
W Yellow "Now run: powershell -File scripts\NUKE-AND-INSTALL.ps1"
W Yellow "Or open http://localhost:3000 and test /finance/accounts"
W Cyan ""
