# Architecture Overview (DEC-103a / DL 78)

> **Status**: System architecture documentation. Reflects state as of 2026-07-10.

---

## 🏛️ High-Level Architecture

```
┌────────────────────────────────────────────────────────────────┐
│                       Browser (User)                            │
└────────────────────────────────────────────────────────────────┘
                              │ HTTPS
                              ▼
┌────────────────────────────────────────────────────────────────┐
│           Caddy Reverse Proxy (HF Spaces)                       │
└────────────────────────────────────────────────────────────────┘
                              │
            ┌─────────────────┴─────────────────┐
            ▼                                   ▼
┌────────────────────────┐         ┌────────────────────────────┐
│  Next.js 14 Frontend  │         │   ASP.NET Core 9 Backend     │
│  (App Router, RTL)     │         │   (Modular Monolith)        │
│  Output: standalone    │         │   ~30 modules, 184 endpoints│
│  33+ pages             │         │   Controllers + Services    │
└────────────────────────┘         └────────────────────────────┘
                                                │
                                                ▼
                                ┌────────────────────────────┐
                                │  PostgreSQL 15 (Neon)       │
                                │  - OLTP schema (business)   │
                                │  - mt_events schema (event) │
                                └────────────────────────────┘
```

---

## 📦 Multi-Tenant Modular Monolith

### Module Structure

```
src/backend/
├── Host/                   # ASP.NET Core entry point + Controllers
│   ├── Controllers/        # 28 controllers, 184 endpoints
│   ├── Program.cs          # DI + middleware config
│   ├── Host/AGENTS.md      # Module conventions
│   └── data-types/         # JSON schema definitions (DEC-079)
│
├── Modules/                # 12 business modules
│   ├── Identity/           # Users, Roles, Tenants, Auth
│   ├── Companies/          # Multi-company + Cost Centers
│   ├── Finance/            # CoA, JEs, TB, Posting Rules
│   ├── AccountsReceivable/ # Customers, Sales Invoices, Receipts
│   ├── Procurement/        # Vendors, POs, GRs, Bills
│   ├── Inventory/          # Items, Categories, UoM, Stock
│   ├── Payments/           # AP/AR payments + allocations
│   ├── Projects/           # Projects + Tasks + Resources
│   ├── HR/                 # Departments, Employees, Leaves
│   ├── Payroll/            # Payroll runs + EOS
│   ├── Notifications/      # User notifications
│   └── Reports/            # Cross-module reports
│
└── Shared/                 # Cross-cutting concerns
    ├── MultiTenancy/       # TenantContext, TenantMiddleware
    ├── Infrastructure/     # DbConnectionFactory, etc.
    ├── Events/             # Domain event publisher
    ├── Audit/              # IAuditLogger
    ├── Migrations/         # FluentMigrator migrations
    ├── SeedData/           # RealisticSeed + JsonSeedLoader
    ├── DataTypes/          # DEC-079 JSON migrator
    └── Generated/          # 🆕 DEC-091/092/102 generated DTOs/Repos
        ├── DTOs/           # 32 .g.cs files
        └── Repos/          # 29 .g.cs files (3 skipped)
```

---

## 🏢 Multi-Tenancy Pattern

### Tenant Resolution Flow

```
1. User logs in → JWT issued with claims:
   - sub: userId
   - tenant_id: Guid
   - email, full_name, role

2. Request arrives → JwtBearer middleware validates token

3. TenantMiddleware extracts claims:
   - Populates ITenantContext (scoped)
   - Sets AsyncLocal for service access

4. Controllers call `_tenant.TenantId` → auto-scoped to user's tenant

5. Repositories filter all queries by `tenant_id`
```

### Public vs Protected Paths

`TenantMiddleware` allows these paths without auth:
- `/health/*`
- `/swagger/*`
- `/api/auth/register`
- `/api/auth/login`
- `/api/auth/refresh`

---

## 📐 DTO + Repository Pattern

### Why DTOs?

- **Decouple** API contract from internal entities
- **Avoid** over-posting (user can't set `created_at`)
- **Stable** API across entity refactors

### Why Repositories?

- **Testability** — swap in mocks
- **Consistent** SQL across entity types
- **Service** layer doesn't know SQL

### Pattern Example

```csharp
// Controller
[HttpGet("api/vendors")]
public async Task<IActionResult> List(...) {
    var result = await _vendorService.ListAsync(tenantId, ...);
    return Ok(result.Value.Select(_mapper.ToDto));
}

// Service
public async Task<VendorListResult> ListAsync(...) {
    var vendors = await _vendorRepository.ListAsync(tenantId, ...);
    return new VendorListResult(vendors);
}

// Repository
public async Task<List<Vendor>> ListAsync(Guid tenantId, ...) {
    using var conn = await _db.CreateOltpConnectionAsync();
    return await conn.QueryAsync<Vendor>(sql, p);
}
```

---

## 🗃️ Database Schema Strategy

### Two Schemas in Same Database

| Schema | Purpose | Tables |
|---|---|---|
| `public` (OLTP) | Business data | All entity tables (50+) |
| `mt_events` | Event store (Sprint-4) | Reserved for MartenDB (DEC-017) |

### Schema-as-Data (DEC-079)

Instead of C# migrations for every change, schema is defined in **JSON files**:

```json
// src/backend/Host/data-types/vendors.json
{
  "name": "Vendor",
  "table": "vendors",
  "fields": [
    { "name": "id", "type": "uuid", "primary_key": true },
    { "name": "tenant_id", "type": "uuid", "foreign_key": {...} },
    ...
  ],
  "indexes": [
    { "name": "ix_vendors_tenant_code", "columns": ["tenant_id", "code"], "unique": true }
  ]
}
```

**Benefit**: `DataTypeMigrator` (DEC-079) reads JSON and applies schema changes idempotently.

### Seed Data (DEC-086)

- 17 JSON files in `data-types/seeds/`
- 14/16 entities actively use `JsonSeedLoader` (DEC-090 audit)
- 2 deferred (BillLines, JELines — complex FK lookups per parent)

---

## 🔐 Auth Architecture

### JWT Flow

```
┌──────────┐  POST /login   ┌──────────┐  Validate   ┌──────────┐
│  Client  │ ──────────────▶│  AuthCtl │ ──────────▶│   DB     │
│          │                │          │             │  (users) │
│          │  accessToken   │          │  Generate   │          │
│          │ ◀──────────────│          │ ◀──────────│          │
└──────────┘                └──────────┘             └──────────┘
     │
     │ Subsequent requests
     ▼
┌──────────┐  Bearer XXX    ┌──────────┐  Parse       ┌──────────┐
│  Client  │ ──────────────▶│ JwtBear │ ──────────▶│  Claims  │
│          │                │          │              │ sub,     │
│          │                │ TenantMw │              │ tenant_id│
│          │                │  + Auth  │              └──────────┘
└──────────┘                └──────────┘
```

### Token Lifetimes

- **Access Token**: 60 minutes
- **Refresh Token**: 14 days (with rotation + reuse detection)
- **Password Reset Token**: 1 hour (DEC-101)

### Password Storage

- **BCrypt** workFactor 11 (login)
- **BCrypt** workFactor 10 (reset tokens)
- Never stored in plaintext

---

## 📊 Event-Driven Architecture (Light)

### Current State (Pre-Sprint-4)

- `IDomainEvent` interface exists
- `DomainEventPublisher` is **scaffold only** (not wired)
- MartenDB is installed but **disabled** (DEC-017)
- Outbox pattern in Postgres: `outbox_events` + `processed_events` tables exist

### Future (Sprint-4)

- MartenDB activation
- Projection building
- Cross-module events (e.g., `VendorCreated` → trigger posting rule)

---

## 🛠️ Tooling & Operations

### Build Pipeline

```
Developer → git push → GitHub Actions:
  ├─ ci-fast.yml   (CI Fast - Tests + Build)  [~2 min]
  ├─ ci-deploy.yml (CI + Deploy)             [~10 min]
  ├─ codeql.yml    (Security Analysis)        [~3 min]
  └─ secrets-scan.yml (TruffleHog)            [~1 min]
```

### Deployment

- **Trigger**: PR merge to `develop` → auto-deploy to HF Space
- **Image**: Docker (multi-stage)
- **CDN**: HF Caddy reverse proxy
- **Health Check**: `/api/health/startup-deep`

### Observability

- **Logging**: Serilog (Console + file rolling)
- **Errors**: Sentry (optional, configured but inactive)
- **Monitoring**: HF dashboard + manual curl probes
- **Bridge**: Cron v2 (HTTP-only, every 6h)

---

## 🛡️ Defense Layers (Cumulative: 78+)

| Phase | DLs | What |
|---|---|---|
| Sprint-1/2 | 1-18 | Foundation, audit, smoke, branch protection |
| Sprint-3 | 19-40 | JSON migration, codegen, defense in depth |
| Sprint-4.5 | 41-50 | Stability, performance |
| Phase 2 | 51-71 | UI pages, auth polish |
| Phase 3 | 72-78 | Codegen, docs |

Each DL is a layer of protection. Examples:
- **DL 67**: Global exception handler (no more empty 500s)
- **DL 68**: Generated DTOs/Repos in production location
- **DL 69**: Payments DI fix (no more missing service errors)
- **DL 70**: Password reset (no more dead-end)
- **DL 71**: Session timeout (no more silent JWT expiry)

---

## 🏗️ Frontend Architecture

### Stack

- **Framework**: Next.js 14.2 (App Router, standalone output)
- **Language**: TypeScript 5.5+ (strict)
- **UI**: Tailwind CSS 3.4 (no shadcn despite AGENTS.md mention)
- **Forms**: Native HTML + custom form components
- **Auth**: `lib/api.ts` (axios + JWT interceptors)
- **State**: React Context (per page)

### Layout

```
src/frontend/
├── app/
│   ├── (authenticated)/     # Protected route group
│   │   ├── dashboard/
│   │   ├── finance/
│   │   ├── procurement/
│   │   ├── inventory/
│   │   ├── hr/
│   │   ├── projects/
│   │   └── admin/
│   ├── login/              # Public
│   │   ├── page.tsx
│   │   ├── forgot/page.tsx
│   │   └── reset/[token]/page.tsx
│   └── layout.tsx
├── components/
│   ├── ui/                 # Reusable: Button, Input, Card, etc.
│   ├── layout/             # AppShell
│   └── SessionTimeoutModal.tsx  (DEC-101)
└── lib/
    ├── api.ts              # Axios + JWT
    ├── useAuth.ts          # Auth hook
    ├── useSessionTimeout.tsx (DEC-101)
    └── utils.ts
```

### Key Patterns

- **RTL**: All UI Arabic-first (dir="rtl")
- **Auth wall**: `(authenticated)/layout.tsx` redirects to `/login`
- **Loading states**: Every page has spinner fallback
- **Error boundaries**: API errors surfaced as friendly Arabic messages

---

## 🛡️ Defense Layer 78: Architecture Documentation Complete

This document provides the high-level mental model for new developers.

Refs: AGENTS.md (root), DEC-053, DEC-079, DEC-091
