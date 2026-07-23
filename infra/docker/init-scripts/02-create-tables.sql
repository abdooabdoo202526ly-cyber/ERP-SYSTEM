-- Auto-generated from data-types/*.json
-- Idempotent (CREATE IF NOT EXISTS) — run after migrations
-- Account (Finance)
CREATE TABLE IF NOT EXISTS accounts (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "code" VARCHAR(50) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "description" VARCHAR(500),
  "type" INTEGER NOT NULL,
  "normal_balance" INTEGER NOT NULL,
  "parent_account_id" UUID REFERENCES accounts(id) ON DELETE SET_NULL,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET_NULL,
  "is_intercompany" BOOLEAN NOT NULL DEFAULT false,
  "is_postable" BOOLEAN NOT NULL DEFAULT true,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_accounts_tenant_code ON accounts("tenant_id", "code");
CREATE  INDEX IF NOT EXISTS ix_accounts_tenant_parent ON accounts("tenant_id", "parent_account_id");
CREATE  INDEX IF NOT EXISTS ix_accounts_company ON accounts("company_id");

-- Attendance (HR)
CREATE TABLE IF NOT EXISTS attendance (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "employee_id" UUID NOT NULL REFERENCES employees(id) ON DELETE CASCADE,
  "type" VARCHAR(20) NOT NULL,
  "timestamp" TIMESTAMPTZ NOT NULL,
  "notes" VARCHAR(500),
  "ip_address" VARCHAR(50),
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE  INDEX IF NOT EXISTS ix_attendance_tenant_employee ON attendance("tenant_id", "employee_id");
CREATE  INDEX IF NOT EXISTS ix_attendance_employee_ts ON attendance("employee_id", "timestamp");

-- AuditLog (Shared)
-- AuditLog (Shared) — BIGSERIAL auto-creates the sequence (fixes 42P01 error on existing volumes)
CREATE SEQUENCE IF NOT EXISTS audit_log_id_seq;
CREATE TABLE IF NOT EXISTS audit_log (
  "id" BIGINT NOT NULL DEFAULT nextval('audit_log_id_seq'::regclass) PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "entity_type" VARCHAR(50) NOT NULL,
  "entity_id" UUID NOT NULL,
  "action" VARCHAR(20) NOT NULL,
  "user_id" UUID,
  "changes" JSONB,
  "ip_address" VARCHAR(45),
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE  INDEX IF NOT EXISTS ix_audit_log_entity ON audit_log("tenant_id", "entity_type", "entity_id", "created_at");
CREATE  INDEX IF NOT EXISTS ix_audit_log_user ON audit_log("tenant_id", "user_id", "created_at");

-- Company (Companies)
CREATE TABLE IF NOT EXISTS companies (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "code" VARCHAR(20) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "legal_name" VARCHAR(200),
  "parent_company_id" UUID REFERENCES companies(id) ON DELETE SET_NULL,
  "is_group" BOOLEAN NOT NULL DEFAULT false,
  "base_currency" VARCHAR(3) NOT NULL DEFAULT 'LYD',
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_companies_tenant_code ON companies("tenant_id", "code");

-- CostCenter (Companies)
CREATE TABLE IF NOT EXISTS cost_centers (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET_NULL,
  "code" VARCHAR(20) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "type" INTEGER NOT NULL,
  "parent_id" UUID REFERENCES cost_centers(id) ON DELETE SET_NULL,
  "budget_amount" NUMERIC(18,4),
  "start_date" TIMESTAMPTZ,
  "end_date" TIMESTAMPTZ,
  "sku" VARCHAR(50),
  "location" TEXT,
  "activity_category" VARCHAR(50),
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_cc_tenant_code ON cost_centers("tenant_id", "code");
CREATE  INDEX IF NOT EXISTS ix_cc_tenant_type ON cost_centers("tenant_id", "type");
CREATE  INDEX IF NOT EXISTS ix_cc_company ON cost_centers("company_id");
CREATE  INDEX IF NOT EXISTS ix_cc_tenant_parent ON cost_centers("tenant_id", "parent_id");

-- Customer (AccountsReceivable)
CREATE TABLE IF NOT EXISTS customers (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET NULL,
  "code" VARCHAR(50) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "name_en" VARCHAR(200),
  "tax_id" VARCHAR(50),
  "email" VARCHAR(200),
  "phone" VARCHAR(50),
  "address" TEXT,
  "credit_limit" NUMERIC(18,4),
  "payment_terms_days" INTEGER NOT NULL DEFAULT 30,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID,
  "deleted_at" TIMESTAMPTZ
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_customers_tenant_code ON customers("tenant_id", "code");
CREATE  INDEX IF NOT EXISTS ix_customers_tenant_company ON customers("tenant_id", "company_id");

-- Department (HR)
CREATE TABLE IF NOT EXISTS departments (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "code" VARCHAR(50) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "parent_id" UUID REFERENCES departments(id) ON DELETE SET_NULL,
  "manager_id" UUID,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_departments_tenant_code ON departments("tenant_id", "code");
CREATE  INDEX IF NOT EXISTS ix_departments_tenant_parent ON departments("tenant_id", "parent_id");

-- Employee (HR)
CREATE TABLE IF NOT EXISTS employees (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "employee_number" VARCHAR(50) NOT NULL,
  "full_name" VARCHAR(200) NOT NULL,
  "email" VARCHAR(200),
  "phone" VARCHAR(50),
  "national_id" VARCHAR(50),
  "department_id" UUID REFERENCES departments(id) ON DELETE SET_NULL,
  "job_title" VARCHAR(100),
  "hire_date" TIMESTAMPTZ NOT NULL,
  "termination_date" TIMESTAMPTZ,
  "base_salary" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_employees_tenant_number ON employees("tenant_id", "employee_number");
CREATE  INDEX IF NOT EXISTS ix_employees_tenant_email ON employees("tenant_id", "email");
CREATE  INDEX IF NOT EXISTS ix_employees_tenant_department ON employees("tenant_id", "department_id");
CREATE  INDEX IF NOT EXISTS ix_employees_tenant_active ON employees("tenant_id", "is_active");

-- ItemCategory (Inventory)
CREATE TABLE IF NOT EXISTS item_categories (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "code" VARCHAR(50) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "description" VARCHAR(500),
  "parent_id" UUID REFERENCES item_categories(id) ON DELETE SET_NULL,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_item_categories_tenant_code ON item_categories("tenant_id", "code");
CREATE  INDEX IF NOT EXISTS ix_item_categories_tenant_parent ON item_categories("tenant_id", "parent_id");

-- Item (Inventory)
CREATE TABLE IF NOT EXISTS items (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET NULL,
  "sku" VARCHAR(50) NOT NULL,
  "barcode" VARCHAR(100),
  "name" VARCHAR(200) NOT NULL,
  "description" TEXT,
  "category_id" UUID REFERENCES item_categories(id) ON DELETE SET_NULL,
  "unit_of_measure_id" UUID REFERENCES units_of_measure(id) ON DELETE SET_NULL,
  "item_type" INTEGER NOT NULL DEFAULT 1,
  "costing_method" INTEGER NOT NULL DEFAULT 3,
  "average_cost" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "standard_cost" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "inventory_account_id" UUID REFERENCES accounts(id) ON DELETE SET_NULL,
  "cogs_account_id" UUID REFERENCES accounts(id) ON DELETE SET_NULL,
  "sales_account_id" UUID REFERENCES accounts(id) ON DELETE SET_NULL,
  "reorder_level" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "reorder_quantity" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_items_tenant_sku ON items("tenant_id", "sku");
CREATE  INDEX IF NOT EXISTS ix_items_tenant_company_active ON items("tenant_id", "company_id", "is_active");
CREATE  INDEX IF NOT EXISTS ix_items_category ON items("category_id");
CREATE  INDEX IF NOT EXISTS ix_items_barcode ON items("barcode");

-- JournalEntry (Finance)
CREATE TABLE IF NOT EXISTS journal_entries (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "entry_number" VARCHAR(50) NOT NULL,
  "entry_date" TIMESTAMPTZ NOT NULL,
  "description" VARCHAR(500) NOT NULL,
  "reference" VARCHAR(200),
  "status" INTEGER NOT NULL DEFAULT 1,
  "created_by_user_id" UUID NOT NULL,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET_NULL,
  "posted_at" TIMESTAMPTZ,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_journal_entries_tenant_number ON journal_entries("tenant_id", "entry_number");
CREATE  INDEX IF NOT EXISTS ix_journal_entries_tenant_date ON journal_entries("tenant_id", "entry_date");
CREATE  INDEX IF NOT EXISTS ix_journal_entries_status ON journal_entries("tenant_id", "status");
CREATE  INDEX IF NOT EXISTS ix_je_company ON journal_entries("company_id");

-- JournalLine (Finance)
CREATE TABLE IF NOT EXISTS journal_lines (
  "id" UUID NOT NULL PRIMARY KEY,
  "journal_entry_id" UUID NOT NULL REFERENCES journal_entries(id) ON DELETE CASCADE,
  "account_id" UUID NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
  "debit" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "credit" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "description" VARCHAR(500),
  "line_number" INTEGER NOT NULL,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET_NULL,
  "cost_center_id" UUID REFERENCES cost_centers(id) ON DELETE SET_NULL
);
CREATE  INDEX IF NOT EXISTS ix_journal_lines_entry ON journal_lines("journal_entry_id");
CREATE  INDEX IF NOT EXISTS ix_journal_lines_account ON journal_lines("account_id");
CREATE  INDEX IF NOT EXISTS ix_jl_company ON journal_lines("company_id");
CREATE  INDEX IF NOT EXISTS ix_jl_cost_center ON journal_lines("cost_center_id");

-- LeaveRequest (HR)
CREATE TABLE IF NOT EXISTS leave_requests (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "employee_id" UUID NOT NULL REFERENCES employees(id) ON DELETE CASCADE,
  "leave_type" VARCHAR(20) NOT NULL,
  "start_date" TIMESTAMPTZ NOT NULL,
  "end_date" TIMESTAMPTZ NOT NULL,
  "total_days" INTEGER NOT NULL,
  "status" VARCHAR(20) NOT NULL DEFAULT 'Pending',
  "reason" TEXT,
  "approver_id" UUID,
  "approved_at" TIMESTAMPTZ,
  "notes" TEXT,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE  INDEX IF NOT EXISTS ix_leaves_tenant_employee ON leave_requests("tenant_id", "employee_id");
CREATE  INDEX IF NOT EXISTS ix_leaves_tenant_status ON leave_requests("tenant_id", "status");
CREATE  INDEX IF NOT EXISTS ix_leaves_employee_dates ON leave_requests("employee_id", "start_date", "end_date");

-- Notification (Inventory)
CREATE TABLE IF NOT EXISTS notifications (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "user_id" UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  "type" VARCHAR(50) NOT NULL,
  "title" VARCHAR(200) NOT NULL,
  "message" TEXT NOT NULL,
  "reference_type" VARCHAR(50),
  "reference_id" UUID,
  "is_read" BOOLEAN NOT NULL DEFAULT false,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "read_at" TIMESTAMPTZ
);
CREATE  INDEX IF NOT EXISTS ix_notifications_tenant_user ON notifications("tenant_id", "user_id");
CREATE  INDEX IF NOT EXISTS ix_notifications_tenant_user_unread ON notifications("tenant_id", "user_id", "is_read");
CREATE  INDEX IF NOT EXISTS ix_notifications_tenant_type ON notifications("tenant_id", "type");
CREATE  INDEX IF NOT EXISTS ix_notifications_tenant_created ON notifications("tenant_id", "created_at");

-- OutboxEvent (Shared)
CREATE TABLE IF NOT EXISTS outbox_events (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "event_type" VARCHAR(100) NOT NULL,
  "aggregate_id" UUID NOT NULL,
  "aggregate_type" VARCHAR(50) NOT NULL,
  "payload" TEXT NOT NULL,
  "occurred_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "processed_at" TIMESTAMPTZ,
  "retry_count" INTEGER NOT NULL DEFAULT 0,
  "max_retries" INTEGER NOT NULL DEFAULT 3,
  "last_error" TEXT
);
CREATE  INDEX IF NOT EXISTS ix_outbox_unprocessed ON outbox_events("occurred_at");
CREATE  INDEX IF NOT EXISTS ix_outbox_tenant_type ON outbox_events("tenant_id", "event_type");
CREATE  INDEX IF NOT EXISTS ix_outbox_processed_at ON outbox_events("processed_at");

-- PasswordResetToken (Identity)
CREATE TABLE IF NOT EXISTS password_reset_tokens (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "user_id" UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  "token_hash" VARCHAR(255) NOT NULL,
  "expires_at" TIMESTAMPTZ NOT NULL,
  "used_at" TIMESTAMPTZ,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_password_reset_token_hash ON password_reset_tokens("token_hash");
CREATE  INDEX IF NOT EXISTS ix_password_reset_user ON password_reset_tokens("user_id");

-- PaymentAllocation (Payments)
CREATE TABLE IF NOT EXISTS payment_allocations (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "payment_id" UUID NOT NULL REFERENCES payments(id) ON DELETE CASCADE,
  "ref_type" VARCHAR(20) NOT NULL,
  "ref_id" UUID NOT NULL,
  "amount_applied" NUMERIC(18,4) NOT NULL
);
CREATE  INDEX IF NOT EXISTS ix_pa_payment ON payment_allocations("payment_id");
CREATE  INDEX IF NOT EXISTS ix_pa_ref ON payment_allocations("ref_type", "ref_id");
CREATE  INDEX IF NOT EXISTS ix_pa_tenant ON payment_allocations("tenant_id");

-- Payment (Payments)
CREATE TABLE IF NOT EXISTS payments (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET_NULL,
  "party_type" VARCHAR(20) NOT NULL,
  "party_id" UUID NOT NULL,
  "payment_number" VARCHAR(50) NOT NULL,
  "payment_date" TIMESTAMPTZ NOT NULL,
  "amount" NUMERIC(18,4) NOT NULL,
  "currency_code" VARCHAR(3) NOT NULL DEFAULT 'LYD',
  "payment_method" VARCHAR(20) NOT NULL DEFAULT 'Cash',
  "bank_account_id" UUID,
  "notes" VARCHAR(1000),
  "status" INTEGER NOT NULL DEFAULT 1,
  "posted_at" TIMESTAMPTZ,
  "posted_by" UUID,
  "journal_entry_id" UUID REFERENCES journal_entries(id) ON DELETE SET_NULL,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_payments_tenant_number ON payments("tenant_id", "payment_number");
CREATE  INDEX IF NOT EXISTS ix_payments_tenant_party ON payments("tenant_id", "party_type", "party_id");
CREATE  INDEX IF NOT EXISTS ix_payments_tenant_status ON payments("tenant_id", "status");
CREATE  INDEX IF NOT EXISTS ix_payments_tenant_date ON payments("tenant_id", "payment_date");

-- PayrollItem (Payroll)
CREATE TABLE IF NOT EXISTS payroll_items (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "payroll_run_id" UUID NOT NULL REFERENCES payroll_runs(id) ON DELETE RESTRICT,
  "employee_id" UUID NOT NULL REFERENCES employees(id) ON DELETE RESTRICT,
  "base_salary" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "gross_salary" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "tax_amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "social_insurance_employee" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "net_salary" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "status" VARCHAR(20) NOT NULL DEFAULT 'Draft',
  "payment_days" INTEGER NOT NULL DEFAULT 30,
  "notes" VARCHAR(500),
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE  INDEX IF NOT EXISTS ix_payroll_items_tenant_run ON payroll_items("tenant_id", "payroll_run_id");
CREATE  INDEX IF NOT EXISTS ix_payroll_items_tenant_employee ON payroll_items("tenant_id", "employee_id");
CREATE  INDEX IF NOT EXISTS ix_payroll_items_tenant_status ON payroll_items("tenant_id", "status");
CREATE UNIQUE INDEX IF NOT EXISTS ix_payroll_items_run_employee ON payroll_items("payroll_run_id", "employee_id");

-- PayrollRun (Payroll)
CREATE TABLE IF NOT EXISTS payroll_runs (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "period_start" TIMESTAMPTZ NOT NULL,
  "period_end" TIMESTAMPTZ NOT NULL,
  "status" VARCHAR(20) NOT NULL DEFAULT 'Draft',
  "total_gross" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "total_net" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "processed_at" TIMESTAMPTZ,
  "posted_at" TIMESTAMPTZ,
  "notes" TEXT,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID
);
CREATE  INDEX IF NOT EXISTS ix_payroll_runs_tenant_status ON payroll_runs("tenant_id", "status");
CREATE  INDEX IF NOT EXISTS ix_payroll_runs_tenant_period ON payroll_runs("tenant_id", "period_start");
CREATE  INDEX IF NOT EXISTS ix_payroll_runs_tenant_created ON payroll_runs("tenant_id", "created_at");

-- PayslipComponent (Payroll)
CREATE TABLE IF NOT EXISTS payslip_components (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "payroll_item_id" UUID NOT NULL REFERENCES payroll_items(id) ON DELETE CASCADE,
  "component_type" VARCHAR(20) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "sort_order" INTEGER NOT NULL DEFAULT 0
);
CREATE  INDEX IF NOT EXISTS ix_payslip_components_tenant_item ON payslip_components("tenant_id", "payroll_item_id");
CREATE  INDEX IF NOT EXISTS ix_payslip_components_item_order ON payslip_components("payroll_item_id", "sort_order");

-- PostingRule (Finance)
CREATE TABLE IF NOT EXISTS posting_rules (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "name" VARCHAR(200) NOT NULL,
  "description" VARCHAR(500),
  "event_type" INTEGER NOT NULL,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "template_json" JSONB NOT NULL,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE  INDEX IF NOT EXISTS ix_posting_rules_tenant_event ON posting_rules("tenant_id", "event_type", "is_active");

-- ProcessedEvent (Shared)
CREATE TABLE IF NOT EXISTS processed_events (
  "event_id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "processed_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE  INDEX IF NOT EXISTS ix_processed_events_tenant ON processed_events("tenant_id");
CREATE  INDEX IF NOT EXISTS ix_processed_events_tenant_processed ON processed_events("tenant_id", "processed_at");

-- Project (Projects)
CREATE TABLE IF NOT EXISTS projects (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET NULL,
  "cost_center_id" UUID NOT NULL,
  "code" VARCHAR(50) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "description" TEXT,
  "customer_id" UUID REFERENCES customers(id) ON DELETE SET_NULL,
  "status" INTEGER NOT NULL DEFAULT 0,
  "budget" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "start_date" TIMESTAMPTZ NOT NULL,
  "end_date" TIMESTAMPTZ,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "deleted_at" TIMESTAMPTZ
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_projects_tenant_code ON projects("tenant_id", "code");
CREATE  INDEX IF NOT EXISTS ix_projects_tenant_company ON projects("tenant_id", "company_id");
CREATE  INDEX IF NOT EXISTS ix_projects_deleted_at ON projects("deleted_at");

-- RefreshToken (Identity)
CREATE TABLE IF NOT EXISTS refresh_tokens (
  "id" UUID NOT NULL PRIMARY KEY,
  "user_id" UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  "token_hash" VARCHAR(500) NOT NULL,
  "expires_at" TIMESTAMPTZ NOT NULL,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "revoked_at" TIMESTAMPTZ,
  "replaced_by_token_hash" VARCHAR(500),
  "revoked_reason" VARCHAR(200),
  "created_by_ip" VARCHAR(45),
  "revoked_by_ip" VARCHAR(45)
);
CREATE  INDEX IF NOT EXISTS ix_refresh_tokens_user ON refresh_tokens("user_id");
CREATE UNIQUE INDEX IF NOT EXISTS ix_refresh_tokens_hash ON refresh_tokens("token_hash");

-- Role (Identity)
CREATE TABLE IF NOT EXISTS roles (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "name" VARCHAR(100) NOT NULL,
  "description" VARCHAR(500) NOT NULL DEFAULT '',
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_roles_tenant_name ON roles("tenant_id", "name");

-- SalaryStructureLine (Payroll)
CREATE TABLE IF NOT EXISTS salary_structure_lines (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "salary_structure_id" UUID NOT NULL REFERENCES salary_structures(id) ON DELETE RESTRICT,
  "type" VARCHAR(20) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "formula" VARCHAR(500),
  "amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "sort_order" INTEGER NOT NULL DEFAULT 0
);
CREATE  INDEX IF NOT EXISTS ix_salary_structure_lines_tenant_structure ON salary_structure_lines("tenant_id", "salary_structure_id");
CREATE  INDEX IF NOT EXISTS ix_salary_structure_lines_structure_order ON salary_structure_lines("salary_structure_id", "sort_order");

-- SalaryStructure (Payroll)
CREATE TABLE IF NOT EXISTS salary_structures (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "name" VARCHAR(200) NOT NULL,
  "code" VARCHAR(50) NOT NULL,
  "currency" VARCHAR(3) NOT NULL DEFAULT 'LYD',
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_salary_structures_tenant_code ON salary_structures("tenant_id", "code");
CREATE  INDEX IF NOT EXISTS ix_salary_structures_tenant_active ON salary_structures("tenant_id", "is_active");

-- StockLevel (Inventory)
CREATE TABLE IF NOT EXISTS stock_levels (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET NULL,
  "item_id" UUID NOT NULL REFERENCES items(id) ON DELETE RESTRICT,
  "warehouse_id" UUID NOT NULL REFERENCES warehouses(id) ON DELETE RESTRICT,
  "quantity_on_hand" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "quantity_reserved" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "average_cost" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "last_movement_at" TIMESTAMPTZ NOT NULL,
  "version" INTEGER NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_stock_levels_tenant_item_warehouse ON stock_levels("tenant_id", "item_id", "warehouse_id");
CREATE  INDEX IF NOT EXISTS ix_stock_levels_tenant_company ON stock_levels("tenant_id", "company_id");

-- StockMovement (Inventory)
CREATE TABLE IF NOT EXISTS stock_movements (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET NULL,
  "reference" VARCHAR(50) NOT NULL,
  "type" INTEGER NOT NULL,
  "movement_date" TIMESTAMPTZ NOT NULL,
  "item_id" UUID NOT NULL REFERENCES items(id) ON DELETE RESTRICT,
  "warehouse_id" UUID NOT NULL REFERENCES warehouses(id) ON DELETE RESTRICT,
  "quantity" NUMERIC(18,4) NOT NULL,
  "unit_cost" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "project_id" UUID REFERENCES projects(id) ON DELETE SET_NULL,
  "cost_center_id" UUID REFERENCES cost_centers(id) ON DELETE SET_NULL,
  "destination_warehouse_id" UUID REFERENCES warehouses(id) ON DELETE SET_NULL,
  "source_type" VARCHAR(50),
  "source_id" UUID,
  "notes" TEXT,
  "status" INTEGER NOT NULL DEFAULT 1,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "posted_at" TIMESTAMPTZ,
  "reversed_by_movement_id" UUID
);
CREATE  INDEX IF NOT EXISTS ix_stock_movements_tenant_status_date ON stock_movements("tenant_id", "status", "movement_date");
CREATE  INDEX IF NOT EXISTS ix_stock_movements_tenant_item_warehouse ON stock_movements("tenant_id", "item_id", "warehouse_id");
CREATE  INDEX IF NOT EXISTS ix_stock_movements_tenant_reference ON stock_movements("tenant_id", "source_type", "source_id");
CREATE  INDEX IF NOT EXISTS ix_stock_movements_tenant_company ON stock_movements("tenant_id", "company_id");

-- StockReservation (Inventory)
CREATE TABLE IF NOT EXISTS stock_reservations (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "item_id" UUID NOT NULL REFERENCES items(id) ON DELETE RESTRICT,
  "warehouse_id" UUID NOT NULL REFERENCES warehouses(id) ON DELETE RESTRICT,
  "quantity" NUMERIC(18,4) NOT NULL,
  "reference_type" VARCHAR(50) NOT NULL,
  "reference_id" UUID NOT NULL,
  "expires_at" TIMESTAMPTZ NOT NULL,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL
);
CREATE  INDEX IF NOT EXISTS ix_stock_reservations_tenant_reference ON stock_reservations("tenant_id", "reference_type", "reference_id");
CREATE  INDEX IF NOT EXISTS ix_stock_reservations_tenant_item_warehouse ON stock_reservations("tenant_id", "item_id", "warehouse_id");
CREATE  INDEX IF NOT EXISTS ix_stock_reservations_expires ON stock_reservations("expires_at");

-- Tenant (Identity)
CREATE TABLE IF NOT EXISTS tenants (
  "id" UUID NOT NULL PRIMARY KEY,
  "name" VARCHAR(200) NOT NULL,
  "subdomain" VARCHAR(100) NOT NULL,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "subscription_expires_at" TIMESTAMPTZ
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_tenants_subdomain ON tenants("subdomain");

-- UnitOfMeasure (Inventory)
CREATE TABLE IF NOT EXISTS units_of_measure (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "code" VARCHAR(20) NOT NULL,
  "name" VARCHAR(100) NOT NULL,
  "symbol" VARCHAR(20),
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_uom_tenant_code ON units_of_measure("tenant_id", "code");

-- UserRole (Identity)
CREATE TABLE IF NOT EXISTS user_roles (
  "user_id" UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  "role_id" UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
  "assigned_at" TIMESTAMPTZ NOT NULL DEFAULT now()
,  PRIMARY KEY (user_id, role_id)
);

-- User (Identity)
CREATE TABLE IF NOT EXISTS users (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "email" VARCHAR(255) NOT NULL,
  "password_hash" VARCHAR(500) NOT NULL,
  "full_name" VARCHAR(200) NOT NULL,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "two_factor_enabled" BOOLEAN NOT NULL DEFAULT false,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "last_login_at" TIMESTAMPTZ
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_users_tenant_email ON users("tenant_id", "email");
CREATE  INDEX IF NOT EXISTS ix_users_email ON users("email");

-- Vendor (Procurement)
CREATE TABLE IF NOT EXISTS vendors (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "code" VARCHAR(50) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "email" VARCHAR(200),
  "phone" VARCHAR(50),
  "address" TEXT,
  "tax_number" VARCHAR(50),
  "website" VARCHAR(200),
  "currency" VARCHAR(3) NOT NULL DEFAULT 'LYD',
  "payment_terms" VARCHAR(20) NOT NULL DEFAULT 'Net30',
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID,
  "deleted_at" TIMESTAMPTZ
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_vendors_tenant_code ON vendors("tenant_id", "code");
CREATE  INDEX IF NOT EXISTS ix_vendors_tenant_active ON vendors("tenant_id", "is_active");
CREATE  INDEX IF NOT EXISTS ix_vendors_tax_number ON vendors("tax_number");

-- Warehouse (Inventory)
CREATE TABLE IF NOT EXISTS warehouses (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET NULL,
  "code" VARCHAR(50) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "location" VARCHAR(500),
  "manager_user_id" UUID,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_warehouses_tenant_code ON warehouses("tenant_id", "code");
CREATE  INDEX IF NOT EXISTS ix_warehouses_tenant_company_active ON warehouses("tenant_id", "company_id", "is_active");


-- ====================================================================
-- PHASE 5 PROCUREMENT (added 2026-07-23 — was missing from auto-gen)
-- ====================================================================

-- PurchaseOrder
CREATE TABLE IF NOT EXISTS purchase_orders (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "po_number" VARCHAR(50) NOT NULL,
  "vendor_id" UUID NOT NULL REFERENCES vendors(id) ON DELETE RESTRICT,
  "status" VARCHAR(20) NOT NULL DEFAULT 'Draft',
  "order_date" TIMESTAMPTZ NOT NULL,
  "expected_date" TIMESTAMPTZ,
  "currency" VARCHAR(3) NOT NULL DEFAULT 'LYD',
  "sub_total" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "tax_amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "total_amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "notes" TEXT,
  "approved_at" TIMESTAMPTZ,
  "approved_by" UUID,
  "sent_at" TIMESTAMPTZ,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_purchase_orders_tenant_ponumber ON purchase_orders("tenant_id", "po_number");
CREATE INDEX IF NOT EXISTS ix_purchase_orders_tenant_vendor ON purchase_orders("tenant_id", "vendor_id");
CREATE INDEX IF NOT EXISTS ix_purchase_orders_tenant_status ON purchase_orders("tenant_id", "status");

-- PurchaseOrderLine
CREATE TABLE IF NOT EXISTS purchase_order_lines (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "purchase_order_id" UUID NOT NULL REFERENCES purchase_orders(id) ON DELETE CASCADE,
  "item_id" UUID NOT NULL REFERENCES items(id) ON DELETE RESTRICT,
  "quantity" NUMERIC(18,4) NOT NULL,
  "unit_price" NUMERIC(18,4) NOT NULL,
  "tax_rate" NUMERIC(6,4) NOT NULL DEFAULT 0,
  "sub_total" NUMERIC(18,4) NOT NULL,
  "line_order" INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_po_lines_po ON purchase_order_lines("purchase_order_id");
CREATE INDEX IF NOT EXISTS ix_po_lines_item ON purchase_order_lines("item_id");

-- GoodsReceipt
CREATE TABLE IF NOT EXISTS goods_receipts (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "gr_number" VARCHAR(50) NOT NULL,
  "purchase_order_id" UUID NOT NULL REFERENCES purchase_orders(id) ON DELETE RESTRICT,
  "status" VARCHAR(20) NOT NULL DEFAULT 'Draft',
  "received_date" TIMESTAMPTZ NOT NULL,
  "warehouse_id" UUID NOT NULL REFERENCES warehouses(id) ON DELETE RESTRICT,
  "notes" TEXT,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_gr_tenant_grnumber ON goods_receipts("tenant_id", "gr_number");
CREATE INDEX IF NOT EXISTS ix_gr_tenant_po ON goods_receipts("tenant_id", "purchase_order_id");
CREATE INDEX IF NOT EXISTS ix_gr_tenant_status ON goods_receipts("tenant_id", "status");

-- GoodsReceiptLine
CREATE TABLE IF NOT EXISTS goods_receipt_lines (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "goods_receipt_id" UUID NOT NULL REFERENCES goods_receipts(id) ON DELETE CASCADE,
  "item_id" UUID NOT NULL REFERENCES items(id) ON DELETE RESTRICT,
  "quantity" NUMERIC(18,4) NOT NULL,
  "unit_cost" NUMERIC(18,4) NOT NULL,
  "notes" TEXT,
  "line_order" INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_gr_lines_gr ON goods_receipt_lines("goods_receipt_id");
CREATE INDEX IF NOT EXISTS ix_gr_lines_item ON goods_receipt_lines("item_id");

-- VendorBill
CREATE TABLE IF NOT EXISTS vendor_bills (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "bill_number" VARCHAR(50) NOT NULL,
  "goods_receipt_id" UUID NOT NULL REFERENCES goods_receipts(id) ON DELETE RESTRICT,
  "vendor_id" UUID NOT NULL REFERENCES vendors(id) ON DELETE RESTRICT,
  "status" VARCHAR(20) NOT NULL DEFAULT 'Draft',
  "bill_date" TIMESTAMPTZ NOT NULL,
  "due_date" TIMESTAMPTZ,
  "currency" VARCHAR(3) NOT NULL DEFAULT 'LYD',
  "sub_total" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "tax_amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "total_amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "notes" TEXT,
  "journal_entry_id" UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
  "posted_at" TIMESTAMPTZ,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_vb_tenant_billnumber ON vendor_bills("tenant_id", "bill_number");
CREATE INDEX IF NOT EXISTS ix_vb_tenant_vendor ON vendor_bills("tenant_id", "vendor_id");
CREATE INDEX IF NOT EXISTS ix_vb_tenant_gr ON vendor_bills("tenant_id", "goods_receipt_id");
CREATE INDEX IF NOT EXISTS ix_vb_tenant_status ON vendor_bills("tenant_id", "status");

-- VendorBillLine
CREATE TABLE IF NOT EXISTS vendor_bill_lines (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "vendor_id" UUID NOT NULL REFERENCES vendors(id) ON DELETE RESTRICT,
  "vendor_bill_id" UUID NOT NULL REFERENCES vendor_bills(id) ON DELETE CASCADE,
  "item_id" UUID NOT NULL REFERENCES items(id) ON DELETE RESTRICT,
  "quantity" NUMERIC(18,4) NOT NULL,
  "unit_price" NUMERIC(18,4) NOT NULL,
  "tax_rate" NUMERIC(6,4) NOT NULL DEFAULT 0,
  "sub_total" NUMERIC(18,4) NOT NULL,
  "line_order" INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_vb_lines_bill ON vendor_bill_lines("vendor_bill_id");
CREATE INDEX IF NOT EXISTS ix_vb_lines_item ON vendor_bill_lines("item_id");

-- ====================================================================
-- PHASE 5 ACCOUNTS RECEIVABLE (added 2026-07-23 — was missing from auto-gen)
-- ====================================================================

-- SalesInvoice
CREATE TABLE IF NOT EXISTS sales_invoices (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET NULL,
  "customer_id" UUID REFERENCES customers(id) ON DELETE SET NULL,
  "invoice_number" VARCHAR(50) NOT NULL,
  "invoice_date" TIMESTAMPTZ NOT NULL,
  "due_date" TIMESTAMPTZ,
  "currency_code" VARCHAR(3) NOT NULL DEFAULT 'LYD',
  "exchange_rate" NUMERIC(18,6) NOT NULL DEFAULT 1,
  "subtotal" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "tax_amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "total_amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "paid_amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "outstanding" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "status" VARCHAR(20) NOT NULL DEFAULT 'Draft',
  "notes" TEXT,
  "project_id" UUID REFERENCES projects(id) ON DELETE SET NULL,
  "posted_at" TIMESTAMPTZ,
  "posted_by" UUID,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID,
  "journal_entry_id" UUID REFERENCES journal_entries(id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_si_tenant_invoiceno ON sales_invoices("tenant_id", "invoice_number");
CREATE INDEX IF NOT EXISTS ix_si_tenant_customer ON sales_invoices("tenant_id", "customer_id");
CREATE INDEX IF NOT EXISTS ix_si_tenant_status ON sales_invoices("tenant_id", "status");
CREATE INDEX IF NOT EXISTS ix_si_tenant_company ON sales_invoices("tenant_id", "company_id");

-- SalesInvoiceLine
CREATE TABLE IF NOT EXISTS sales_invoice_lines (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "sales_invoice_id" UUID NOT NULL REFERENCES sales_invoices(id) ON DELETE CASCADE,
  "item_id" UUID REFERENCES items(id) ON DELETE SET NULL,
  "description" VARCHAR(500) NOT NULL,
  "line_number" INTEGER NOT NULL DEFAULT 0,
  "quantity" NUMERIC(18,4) NOT NULL,
  "unit_price" NUMERIC(18,4) NOT NULL,
  "tax_rate" NUMERIC(6,4) NOT NULL DEFAULT 0,
  "line_total" NUMERIC(18,4) NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_si_lines_invoice ON sales_invoice_lines("sales_invoice_id");
CREATE INDEX IF NOT EXISTS ix_si_lines_item ON sales_invoice_lines("item_id");

-- Receipt
CREATE TABLE IF NOT EXISTS receipts (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "company_id" UUID REFERENCES companies(id) ON DELETE SET NULL,
  "customer_id" UUID NOT NULL REFERENCES customers(id) ON DELETE RESTRICT,
  "receipt_number" VARCHAR(50) NOT NULL,
  "receipt_date" TIMESTAMPTZ NOT NULL,
  "amount" NUMERIC(18,4) NOT NULL,
  "currency_code" VARCHAR(3) NOT NULL DEFAULT 'LYD',
  "payment_method" VARCHAR(20),
  "notes" TEXT,
  "posted_at" TIMESTAMPTZ,
  "posted_by" UUID,
  "journal_entry_id" UUID REFERENCES journal_entries(id) ON DELETE SET NULL,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "created_by" UUID NOT NULL,
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_by" UUID
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_receipts_tenant_receiptno ON receipts("tenant_id", "receipt_number");
CREATE INDEX IF NOT EXISTS ix_receipts_tenant_customer ON receipts("tenant_id", "customer_id");
CREATE INDEX IF NOT EXISTS ix_receipts_tenant_company ON receipts("tenant_id", "company_id");

-- ReceiptAllocation
CREATE TABLE IF NOT EXISTS receipt_allocations (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "receipt_id" UUID NOT NULL REFERENCES receipts(id) ON DELETE CASCADE,
  "sales_invoice_id" UUID NOT NULL REFERENCES sales_invoices(id) ON DELETE CASCADE,
  "amount_applied" NUMERIC(18,4) NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_ra_receipt ON receipt_allocations("receipt_id");
CREATE INDEX IF NOT EXISTS ix_ra_invoice ON receipt_allocations("sales_invoice_id");
CREATE INDEX IF NOT EXISTS ix_ra_tenant ON receipt_allocations("tenant_id");

-- ====================================================================
-- PROJECTS (added 2026-07-23 — was missing from auto-gen)
-- ====================================================================

-- ProjectBudget
CREATE TABLE IF NOT EXISTS project_budgets (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "project_id" UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  "cost_center_id" UUID NOT NULL REFERENCES cost_centers(id) ON DELETE RESTRICT,
  "account_id" UUID REFERENCES accounts(id) ON DELETE SET NULL,
  "budget_amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "spent_amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "committed_amount" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "last_recalculated_at" TIMESTAMPTZ
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_pb_project ON project_budgets("project_id");
CREATE INDEX IF NOT EXISTS ix_pb_costcenter ON project_budgets("cost_center_id");
CREATE INDEX IF NOT EXISTS ix_pb_tenant ON project_budgets("tenant_id");

-- ProjectTask
CREATE TABLE IF NOT EXISTS project_tasks (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "project_id" UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  "name" VARCHAR(200) NOT NULL,
  "description" TEXT,
  "status" INTEGER NOT NULL DEFAULT 1,
  "estimated_hours" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "actual_hours" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "start_date" TIMESTAMPTZ,
  "end_date" TIMESTAMPTZ,
  "progress_percent" INTEGER NOT NULL DEFAULT 0,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_pt_project ON project_tasks("project_id");
CREATE INDEX IF NOT EXISTS ix_pt_tenant ON project_tasks("tenant_id");

-- Resource
CREATE TABLE IF NOT EXISTS resources (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "code" VARCHAR(50) NOT NULL,
  "name" VARCHAR(200) NOT NULL,
  "type" INTEGER NOT NULL DEFAULT 1,
  "hourly_rate" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "is_active" BOOLEAN NOT NULL DEFAULT true,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now(),
  "updated_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_resources_tenant_code ON resources("tenant_id", "code");
CREATE INDEX IF NOT EXISTS ix_resources_tenant_active ON resources("tenant_id", "is_active");

-- ResourceAssignment
CREATE TABLE IF NOT EXISTS resource_assignments (
  "id" UUID NOT NULL PRIMARY KEY,
  "tenant_id" UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
  "project_id" UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
  "task_id" UUID NOT NULL REFERENCES project_tasks(id) ON DELETE CASCADE,
  "resource_id" UUID NOT NULL REFERENCES resources(id) ON DELETE RESTRICT,
  "user_id" UUID REFERENCES users(id) ON DELETE SET NULL,
  "from_ts" TIMESTAMPTZ NOT NULL,
  "to_ts" TIMESTAMPTZ NOT NULL,
  "hourly_rate" NUMERIC(18,4) NOT NULL DEFAULT 0,
  "created_at" TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX IF NOT EXISTS ix_ras_project ON resource_assignments("project_id");
CREATE INDEX IF NOT EXISTS ix_ras_task ON resource_assignments("task_id");
CREATE INDEX IF NOT EXISTS ix_ras_resource ON resource_assignments("resource_id");
CREATE INDEX IF NOT EXISTS ix_ras_tenant ON resource_assignments("tenant_id");

-- End of additions — total tables: 51
