# CRUD Audit — ERP-SYSTEM (2026-07-23)

This document captures the current state of every entity's CRUD coverage as of v1.0.10.

## Quick Test

```bash
bash scripts/e2e-smoke-test.sh
```

Output is colored: green = pass, red = fail, yellow = skip.

## Status by Module

### Identity & Auth
- `auth/login`, `auth/register`, `auth/refresh` — all OK
- `auth/me` — OK
- `users` CRUD — partial (admin can list, but no self-service edit endpoint)
- `roles` — list/create OK, edit/delete TODO

### Finance
- **Accounts (Chart of Accounts)** — ✅ Full CRUD + cycle detection on update
- Journal Entries — read + create only (immutable by design)
- Posting Rules — read + create (edit/delete TODO)
- Reports — read-only (no CRUD)

### AR
- Customers — ✅ Full CRUD
- Sales Invoices — ✅ Full CRUD + Post workflow
- Receipts — ✅ Full CRUD + Post workflow

### Procurement
- Vendors, POs, GRs, Vendor Bills — ✅ Full CRUD

### Inventory
- Items, Warehouses — ✅ Full CRUD
- Item Categories — CRUD mostly OK (delete TODO)
- **UoM** — ✅ Full CRUD (v1.0.10 added Update + Deactivate)
- Stock Levels — read-only (calculated from movements)
- Stock Movements — read + create (immutable ledger)
- Stock Reservations — create + delete (read OK)

### HR
- Employees, Departments, Leaves, Salary Structures — ✅ Full CRUD
- Attendance — read + create (edit/delete TODO)
- Payroll Runs, Payslips — read + create (edit/delete TODO)

### Payments
- Payments — read + create (update/delete TODO)
- Payment Allocations — read + create

### Projects
- Projects, Tasks — ✅ Full CRUD
- Project Budgets, Resources, Resource Assignments — partial

### Companies
- **Companies** — ✅ Full CRUD (v1.0.10 added Update)
- **Cost Centers** — ✅ Full CRUD (v1.0.10 added Update)

### Admin
- Notifications — read + create (mark-as-read TODO)

## Known Issues / Punted

| Issue | Where | Status | Workaround |
|-------|-------|--------|-----------|
| Customers page 403 | `/finance/customers` list | Mystery | Restart API after v1.0.9 applies (DataTypeMigrator fix) |
| Hydration mismatch on `<Input>` | All forms with Input | ✅ FIXED in v1.0.10 | useId() hook |
| `/api/finance/accounts` 500 | When NULL parent | ✅ FIXED in v1.0.10 | UpdateAsync handles null parents |
| RSC fetch 403 on prefetch | `/finance/accounts?_rsc=` | Likely | Refresh page after login |

## Next Sprint Priorities

1. **Notifications mark-as-read** (PUT /api/admin/notifications/{id})
2. **Attendance edit/delete** (HR)
3. **Payroll lifecycle endpoints** (HR)
4. **Items category delete** (Inventory)
5. **Project budget/resources** update (Projects)
6. **Stock reservation edit** (Inventory)
