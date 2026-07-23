---
title: ERP-SYSTEM
emoji: 🏢
colorFrom: blue
colorTo: indigo
sdk: docker
app_port: 7860
pinned: false
license: mit
short_description: Multi-tenant ERP (Finance + HR + Payroll)
---

# ERP-SYSTEM

A complete **Multi-Tenant Modular Monolith ERP system** with:

- 💰 **Finance**: Chart of Accounts, Journal Entries, General Ledger, Posting Rules
- 📦 **Inventory**: Items, Warehouses, Stock Movements (CQRS), Low-stock alerts
- 📊 **Projects**: Project Management, Tasks, Resources, Budgets
- 👥 **HR + Payroll**: Employees, Attendance, Leaves, Payroll engine, EOS
- 💳 **Payments**: AP/AR Payments, Allocations
- 🧾 **Accounts Receivable**: Customers, Sales Invoices, Receipts, Aging reports
- 🛒 **Procurement**: Purchase Orders, Goods Receipts, Vendor Bills
- 🔄 **Event Sourcing**: Outbox pattern + Postgres LISTEN/NOTIFY (MartenDB event store **planned Sprint-5+**)
- 🔐 **JWT + Multi-tenancy**: Full isolation per tenant

## 🔜 Roadmap: Event Sourcing (Sprint-5+)

The system is configured to use **MartenDB** for event sourcing but the feature is currently **disabled** (DEC-017).

To enable in Sprint-5+:
1. Uncomment `AddMarten()` block in `src/backend/Host/Program.cs`
2. Add `IDocumentSession` to `OutboxEventPublisher`
3. Create projections for materialized views

Why deferred: Event sourcing adds complexity. Feature flag infrastructure (Sprint-4) needed first.
Reference: `DEC-017` (2026-07-05)

## 🏗️ Architecture

```
Internet → :7860 (Caddy reverse proxy)
              ├── /api/*     → :5000 (ASP.NET Core 9 API)
              └── /*         → :3000 (Next.js 14 Frontend)
```

The container runs **3 processes** managed by `supervisord`:
- **API** (.NET 9) on port 5000
- **Frontend** (Next.js 14) on port 3000
- **Caddy** (reverse proxy) on port 7860 (public)

## 🔧 Required Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `DB_CONNECTION` | OLTP database connection string | `postgresql://neondb_owner:PASSWORD@ep-xxx.aws.neon.tech/neondb?sslmode=require` (Neon's default `neondb`) |
| `EVENTS_CONNECTION` | Optional: Event store (Marten, currently disabled — see DEC-017) | `postgresql://neondb_owner:PASSWORD@ep-xxx.aws.neon.tech/erp_events?sslmode=require` |
| `JWT_SECRET` | JWT signing secret (min 64 chars) | `your-64-character-secret-here-replace-this` |

### 📝 How to set:

1. Go to your Space **Settings** → **Variables and secrets**
2. Add each variable above with the appropriate value
3. Restart the Space

## 🗄️ Database Setup (PostgreSQL)

The system needs a PostgreSQL 15+ database. **Hugging Face Spaces does not provide a database**, so you must use an external one. Here are the best free options:

### Option 1: **Neon.tech** (Recommended) ⭐
- Free tier: 0.5 GB storage, 190 compute hours/month
- Serverless Postgres with auto-scaling
- Built-in connection pooling
- Branch databases (great for dev/staging)
- **Steps**:
  1. Sign up at https://neon.tech
  2. Create a new project
  3. Create an **Event Store database** (optional — Marten is disabled per DEC-017):
     - Recommended name: `erp_events` (underscore)
     - Skip this if you don't plan to enable Marten in Sprint-5+
  4. Copy the connection string for each (Neon creates a default `neondb` automatically)
  4. Copy the connection string for each
  5. Set as environment variables above

### Option 2: **Supabase**
- Free tier: 500 MB storage, unlimited API requests
- Built-in dashboard + SQL editor
- Connection string with pooling available

### Option 3: **Railway PostgreSQL**
- $1/month after trial (essentially free for low-traffic)
- One-click setup

## 🚀 First-time Setup

When the Space starts, the **MigrationRunnerHostedService** will automatically:
1. Connect to your PostgreSQL
2. Apply all 14 migrations to both databases
3. Create all tables (Identity, Finance, Projects, HR, Payroll, etc.)
4. Seed default data (CoA, etc.)

You can then:
1. Open the Space URL in your browser
2. Click "Register" to create your first tenant + admin user
3. Start using the system!

## 🔐 Default Test Users

After registering, you can log in with the email/password you created. The first user becomes the **Admin** of a new tenant.

## 🩺 Health Endpoints (Sprint-4 Day 1)

| Endpoint | Purpose | Auth |
|----------|---------|------|
| `GET /api/health/live` | Liveness probe — is the process alive? | No |
| `GET /api/health/startup` | Startup probe — has the process started? | No |
| `GET /api/health/startup-deep` | Deep diagnostics — DB, migrations, config | No |

Useful for Kubernetes liveness/readiness probes, uptime monitoring (UptimeRobot, Pingdom), or quick status checks.

Example:
```bash
curl -s https://anas-assaket-erp-system.hf.space/api/health/startup-deep | jq
```

## 🛠️ Admin Endpoints (Sprint-4 Day 2)

Admin-only manual triggers for sensitive operations. Requires JWT with `Admin` role.

| Method | Endpoint | Purpose |
|--------|----------|---------|
| `POST` | `/api/admin/seed/alfajr` | Trigger AlFajr scenario seeder in background (~5K records) |
| `POST` | `/api/admin/seed/alburj` | Returns 501 Not Implemented (DEC-009 prevention — class deleted) |
| `GET` | `/api/admin/seed/status/{jobId}` | Poll job status (queued/running/completed/failed) |
| `GET` | `/api/admin/seed/jobs` | List last 20 jobs (admin audit trail) |

Example:
```bash
# 1. Login as Admin to get JWT
TOKEN=$(curl -s -X POST https://.../api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@alfajr.local","password":"Demo1234"}' | jq -r .accessToken)

# 2. Trigger manual seed
JOB=$(curl -s -X POST https://.../api/admin/seed/alfajr \
  -H "Authorization: Bearer $TOKEN" | jq -r .jobId)

# 3. Poll status
curl -s https://.../api/admin/seed/status/$JOB \
  -H "Authorization: Bearer $TOKEN"
```

## 🔍 Observability (Sprint-4 Day 3)

### Structured JSON Logging

- **Development**: human-readable output (`[{HH:mm:ss} {Level}] {Source}: {message}`)
- **Production**: `CompactJsonFormatter` (one JSON object per line) — ready for Loki/Elasticsearch/CloudWatch

Every log entry is enriched with:
- `RequestId` (from `X-Request-ID` header, or auto-generated)
- `TenantId` (from JWT claim, if authenticated)
- `UserId` (from JWT claim, if authenticated)
- `Method`, `Path`, `MachineName`, `ThreadId`, `Application`, `Environment`

### Request Tracking

Every request gets:
- `X-Request-ID` header (generated or echoed from incoming)
- Logged with method/path/status/elapsed-ms
- Available via `HttpContext.Items["RequestId"]` for downstream code

### Sentry Integration (Optional)

Enable by setting the `Sentry__Dsn` environment variable in your Space:
- Disabled by default (no DSN = no-op, no overhead)
- When enabled: 20% transaction sampling (HF free tier friendly)
- GDPR-safe defaults (`SendDefaultPii=false`)

## 🌿 Deployment Pipeline (Sprint-4 Day 4)

The repository uses **branch-based environments**:

| Branch | Environment | Auto-deploy |
|--------|-------------|-------------|
| `develop` | **Staging** | ✅ Auto-sync to HF Space on every push |
| `main` | **Production** | ⚠️ Manual deploy via `Actions → Deploy` (or merge from develop) |

Workflows:
- `.github/workflows/ci.yml` — basic checks
- `.github/workflows/deploy.yml` — tests + sync (recommended for PRs)
- `.github/workflows/sync-to-hf-space.yml` — direct sync (manual trigger or push to develop/main)

## 📊 Tech Stack

- **Backend**: C# / .NET 9, Dapper, FluentMigrator, MartenDB
- **Frontend**: Next.js 14, TypeScript, Tailwind CSS, TanStack Query
- **Database**: PostgreSQL 15+ (external)
- **Auth**: JWT + BCrypt + Refresh tokens
- **Events**: Postgres LISTEN/NOTIFY + Outbox pattern
- **CI/CD**: GitHub Actions

## 📜 License

MIT

---

## 📚 Documentation (DEC-103a / DL 78)

Comprehensive documentation:

| Doc | Description | Link |
|---|---|---|
| **AGENTS.md** | AI Agent + human conventions | [AGENTS.md](./AGENTS.md) |
| **Architecture** | High-level system design, modules, multi-tenancy, DTO/Repo pattern | [docs/dec-103a/ARCHITECTURE.md](./docs/dec-103a/ARCHITECTURE.md) |
| **API Reference** | All 184 endpoints across 28 controllers | [docs/dec-103a/API.md](./docs/dec-103a/API.md) |
| **Performance Audit** | DB indexes, query patterns, connection pool, caching | [docs/dec-103a/PERFORMANCE-AUDIT.md](./docs/dec-103a/PERFORMANCE-AUDIT.md) |
| **Phase 2 Report** | UI/API inventory + DEC-094/095/096/097/098 results | [PHASE2-REPORT.md](./PHASE2-REPORT.md) |
| **STATUS** | Current system state | [STATUS.md](./STATUS.md) |
| **Runbook** | Operational procedures | [RUNBOOK.md](./RUNBOOK.md) |

## 📊 System State (2026-07-10)

| Metric | Value |
|---|---|
| Defense Layers | 78+ |
| Frontend Pages | 33+ |
| Backend Endpoints | 184 |
| Modules | 12 |
| Seed Entities (JSON) | 14/16 active (87.5%) |
| Sprint Status | Sprint-3 closed, Phase 2 closed, Phase 3 (docs) complete |
| Score | **98%** |
