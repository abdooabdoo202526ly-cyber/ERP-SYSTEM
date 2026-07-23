# API Reference (DEC-103a / DL 77)

> **Status**: Complete inventory of 184 endpoints across 24 controllers.

**Base URL**: `https://Anas-Assaket-erp-system.hf.space` (case-sensitive!)

**Auth**: Bearer JWT (60min) + Refresh Token (14 days). See [Authentication](#authentication).

---

## 📋 Quick Reference

| Method | Count | Notes |
|---|---|---|
| GET | ~80 | Read endpoints (most require auth) |
| POST | ~70 | Create + action endpoints |
| PUT | ~20 | Update endpoints |
| DELETE | ~14 | Delete/deactivate |

---

## 🔐 Authentication

All endpoints under `/api/*` (except `/api/auth/*` and `/api/health/*`) require Bearer JWT.

### POST `/api/auth/register`
Create new tenant + admin user.

**Body**:
```json
{
  "tenantName": "string",
  "subdomain": "string (optional)",
  "baseCurrency": "LYD",
  "adminEmail": "user@example.com",
  "adminPassword": "Demo1234!",
  "adminFullName": "John Doe"
}
```

**Response 200**:
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "...",
  "user": { "id": "...", "tenantId": "...", "email": "...", "fullName": "...", "roles": ["Admin"] }
}
```

### POST `/api/auth/login`
Login with existing credentials.

**Body**:
```json
{ "email": "admin@alfajr.local", "password": "Demo1234" }
```

**Response 200**: Same as register.

### POST `/api/auth/refresh`
Get new access token using refresh token.

**Body**:
```json
{ "refreshToken": "..." }
```

### POST `/api/auth/forgot-password` 🆕 (DEC-101)
Request password reset email.

**Body**:
```json
{ "email": "user@example.com" }
```

**Response 200**: Always returns 200 (no user enumeration).

### POST `/api/auth/reset-password` 🆕 (DEC-101)
Reset password with token.

**Body**:
```json
{ "token": "abc123...", "newPassword": "NewPass123" }
```

### POST `/api/auth/logout`
Revoke refresh token. Requires auth.

### GET `/api/auth/me`
Get current user info. Requires auth.

---

## 💚 Health

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/health/live` | No | Liveness check |
| GET | `/api/health/startup-deep` | No | Startup + DB ping |
| GET | `/api/health/{*}` | No | Various health endpoints |

---

## 👥 Identity

| Method | Path | Auth | Description |
|---|---|---|---|
| (User mgmt via /api/auth/*) | | | |
| GET | `/api/tenants` | Yes | List tenants (system) |
| GET | `/api/users` | Yes | List users in tenant |
| GET | `/api/roles` | Yes | List roles |
| (Tenant mgmt via /api/companies) | | | |

---

## 🏢 Companies & Multi-Company

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/companies` | Yes | List companies in tenant |
| GET | `/api/companies/{id}` | Yes | Get company by id |
| POST | `/api/companies` | Yes | Create company |
| PUT | `/api/companies/{id}` | Yes | Update company |
| DELETE | `/api/companies/{id}` | Yes | Deactivate |

---

## 🏗️ Cost Centers

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/cost-centers` | Yes | List cost centers |
| GET | `/api/cost-centers/{id}` | Yes | Get by id |
| GET | `/api/cost-centers/{id}/children` | Yes | Get child cost centers |
| GET | `/api/cost-centers/{id}/budget-status` | Yes | Budget vs actual |
| POST | `/api/cost-centers` | Yes | Create |
| DELETE | `/api/cost-centers/{id}` | Yes | Deactivate |

---

## 💰 Finance

### Accounts (CoA)

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/finance/accounts` | Yes | List accounts |
| GET | `/api/finance/accounts/{id}` | Yes | Get by id |
| GET | `/api/finance/accounts/by-code/{code}` | Yes | Get by code |
| POST | `/api/finance/accounts` | Yes | Create account |
| DELETE | `/api/finance/accounts/{id}` | Yes | Deactivate |

### Journal Entries

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/finance/journal-entries` | Yes | List with filters |
| GET | `/api/finance/journal-entries/{id}` | Yes | Get with lines |
| POST | `/api/finance/journal-entries` | Yes | Create (draft, balanced) |
| POST | `/api/finance/journal-entries/{id}/post` | Yes | Post to GL |

### Posting Rules

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/finance/posting-rules` | Yes | List rules |
| GET | `/api/finance/posting-rules/{id}` | Yes | Get rule |
| POST | `/api/finance/posting-rules` | Yes | Create |
| POST | `/api/finance/posting-rules/trigger/{eventType}` | Yes | Trigger rule |

### Ledger

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/finance/ledger/trial-balance` | Yes | TB report |
| GET | `/api/finance/ledger/accounts/{id}` | Yes | Account ledger |

### Reports

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/reports/finance/trial-balance?asOfDate=YYYY-MM-DD` | Yes | TB by date |
| GET | `/api/ar/aging` | Yes | AR aging |
| GET | `/api/reports/finance/*` | Yes | Other finance reports |

---

## 🧾 AR (Accounts Receivable)

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/ar/customers` | Yes | List customers |
| GET | `/api/ar/customers/{id}` | Yes | Get customer |
| POST | `/api/ar/customers` | Yes | Create |
| PUT | `/api/ar/customers/{id}` | Yes | Update |
| GET | `/api/ar/sales-invoices` | Yes | List invoices |
| GET | `/api/ar/sales-invoices/{id}` | Yes | Get invoice |
| POST | `/api/ar/sales-invoices` | Yes | Create |
| POST | `/api/ar/sales-invoices/{id}/post` | Yes | Post invoice |
| POST | `/api/ar/sales-invoices/{id}/cancel` | Yes | Cancel |
| GET | `/api/ar/receipts` | Yes | List receipts |
| GET | `/api/ar/receipts/{id}` | Yes | Get receipt |
| POST | `/api/ar/receipts` | Yes | Create |
| POST | `/api/ar/receipts/{id}/post` | Yes | Post |
| POST | `/api/ar/receipts/{id}/reverse` | Yes | Reverse |

---

## 📦 Procurement (AP)

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/procurement/vendors` | Yes | List vendors |
| GET | `/api/procurement/vendors/{id}` | Yes | Get vendor |
| POST | `/api/procurement/vendors` | Yes | Create |
| PUT | `/api/procurement/vendors/{id}` | Yes | Update |
| GET | `/api/procurement/pos` | Yes | List POs |
| GET | `/api/procurement/pos/{id}` | Yes | Get PO |
| POST | `/api/procurement/pos` | Yes | Create |
| POST | `/api/procurement/pos/{id}/approve` | Yes | Approve |
| POST | `/api/procurement/pos/{id}/send` | Yes | Send to vendor |
| GET | `/api/procurement/grs` | Yes | List GRs |
| GET | `/api/procurement/grs/{id}` | Yes | Get GR |
| POST | `/api/procurement/grs/{id}/receive` | Yes | Receive goods |
| GET | `/api/procurement/bills` | Yes | List bills |
| GET | `/api/procurement/bills/{id}` | Yes | Get bill |
| POST | `/api/procurement/bills` | Yes | Create |
| POST | `/api/procurement/bills/{id}/post` | Yes | Post to AP |

---

## 🏭 Inventory

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/inventory/items` | Yes | List items |
| GET | `/api/inventory/items/{id}` | Yes | Get item |
| POST | `/api/inventory/items` | Yes | Create |
| PUT | `/api/inventory/items/{id}` | Yes | Update |
| GET | `/api/inventory/categories` | Yes | List categories |
| GET | `/api/inventory/categories/{id}` | Yes | Get category |
| GET | `/api/inventory/categories/{id}/children` | Yes | Get children |
| POST | `/api/inventory/categories` | Yes | Create |
| PUT | `/api/inventory/categories/{id}` | Yes | Update |
| GET | `/api/inventory/uom` | Yes | List UoMs |
| GET | `/api/inventory/warehouses` | Yes | List warehouses |
| GET | `/api/inventory/levels` | Yes | Stock levels |
| GET | `/api/inventory/levels/items/{itemId}` | Yes | By item |
| GET | `/api/inventory/levels/warehouses/{warehouseId}` | Yes | By warehouse |
| GET | `/api/inventory/levels/low-stock` | Yes | Low stock alert |
| GET | `/api/inventory/movements` | Yes | List movements |
| GET | `/api/inventory/movements/{id}` | Yes | Get movement |
| POST | `/api/inventory/movements/receive` | Yes | Receive stock |
| POST | `/api/inventory/movements/issue` | Yes | Issue stock |
| POST | `/api/inventory/movements/transfer` | Yes | Transfer |
| POST | `/api/inventory/movements/adjust` | Yes | Adjust |
| POST | `/api/inventory/movements/{id}/post` | Yes | Post |
| POST | `/api/inventory/movements/{id}/reverse` | Yes | Reverse |
| GET | `/api/inventory/reservations` | Yes | List reservations |
| POST | `/api/inventory/reservations` | Yes | Create |
| DELETE | `/api/inventory/reservations/{id}` | Yes | Cancel |
| GET | `/api/inventory/notifications` | Yes | User notifications |
| GET | `/api/inventory/notifications/unread` | Yes | Unread only |
| POST | `/api/inventory/notifications/{id}/mark-read` | Yes | Mark read |

---

## 💳 Payments (AP + AR)

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/payments` | Yes | List payments |
| GET | `/api/payments/{id}` | Yes | Get payment |
| POST | `/api/payments` | Yes | Create payment |
| POST | `/api/payments/{id}/post` | Yes | Post + create JE |
| POST | `/api/payments/{id}/allocate` | Yes | Add allocations |

**Note**: `/api/payments` was 500 in early Sprint-3 (DL 69) — fixed by adding IPaymentService to DI.

---

## 👥 HR

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/hr/departments` | Yes | List |
| GET | `/api/hr/departments/{id}` | Yes | Get |
| POST | `/api/hr/departments` | Yes | Create |
| GET | `/api/hr/employees` | Yes | List |
| GET | `/api/hr/employees/{id}` | Yes | Get |
| POST | `/api/hr/employees` | Yes | Create |
| PUT | `/api/hr/employees/{id}` | Yes | Update |
| GET | `/api/hr/leaves` | Yes | List |
| GET | `/api/hr/leaves/{id}` | Yes | Get |
| POST | `/api/hr/leaves` | Yes | Submit |
| POST | `/api/hr/leaves/{id}/approve` | Yes | Approve |
| POST | `/api/hr/leaves/{id}/reject` | Yes | Reject |
| GET | `/api/hr/attendance` | Yes | List |
| GET | `/api/hr/attendance/{id}` | Yes | Get |
| POST | `/api/hr/attendance` | Yes | Mark attendance |

---

## 💵 Payroll

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/hr/payroll/runs` | Yes | List payroll runs |
| GET | `/api/hr/payroll/runs/{id}` | Yes | Get run |
| POST | `/api/hr/payroll/runs` | Yes | Create run |
| POST | `/api/hr/payroll/runs/{id}/process` | Yes | Process |
| POST | `/api/hr/payroll/runs/{id}/post` | Yes | Post |
| GET | `/api/hr/payroll/runs/{id}/items` | Yes | Run items |
| GET | `/api/hr/payroll/runs/{id}/items/{empId}/payslip` | Yes | Payslip |
| GET | `/api/hr/payroll/eos/{empId}` | Yes | EOS calc |

---

## 📊 Projects

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/projects` | Yes | List projects |
| GET | `/api/projects/{id}` | Yes | Get project |
| POST | `/api/projects` | Yes | Create |
| PUT | `/api/projects/{id}` | Yes | Update |
| GET | `/api/tasks` | Yes | List tasks |
| POST | `/api/tasks` | Yes | Create |
| GET | `/api/resources` | Yes | List resources |

---

## 🔔 Notifications

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/inventory/notifications` | Yes | All notifications |
| GET | `/api/inventory/notifications/unread` | Yes | Unread count |
| POST | `/api/inventory/notifications/{id}/mark-read` | Yes | Mark read |

---

## 🛠️ Admin

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/admin/finance/backfill` | Yes | Fire-and-forget backfill |
| POST | `/api/admin/finance/backfill` | Yes | (Same) |
| GET | `/api/debug/seed-status` | Yes | Per-step seed state |
| GET | `/api/events/*` | Yes | Domain events |

---

## 🩺 Health Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/health/live` | No | Always 200 if process alive |
| GET | `/api/health/startup-deep` | No | Includes DB ping |
| GET | `/health` | No | Simple 200 |

---

## 📊 Response Formats

### Success
```json
{ "data": "...", "message": "..." }
```

### Error (ProblemDetails)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "Specific error message"
}
```

### Pagination
List endpoints support `?skip=0&take=50` (default 50, max 200).

### Multi-Tenancy
All resources scoped to `tenant_id` from JWT claim. Auto-filtered by `TenantMiddleware`.

---

## 🛡️ Defense Layer 77: API Documentation Complete

Refs: Sprint-3, DEC-091-103

**Note**: This is a generated inventory from controller files. For live testing, see:
- `scripts/smoke-test.sh` (40 endpoints)
- `scripts/workflow-test.sh` (5 workflows with auth)
