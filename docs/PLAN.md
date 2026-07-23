# خطة تطوير وتنفيذ النظام (ERP-SYSTEM)

> **الإصدار:** 2.0
> **التاريخ:** 2026-06-16
> **الحالة:** Phase 0 → 4 مكتملة ✅ · Phase 5 قادم (Mobile App + AlFajr Demo Data)
> **المنهجية:** Agile (Scrum) + Iterative MVP + GitFlow
> **الهدف:** نظام ERP متعدد المستأجرين، Modular Monolith، قابل للإنتاج

---

## 📊 ملخص التحديث (v1.0 → v2.0)

الخطة الأصلية (v1.0) كانت في 13 يونيو 2026. منذ ذلك الحين:

| ما تغيّر | من | إلى |
|---------|-----|-----|
| **.NET Version** | 8 | **9** |
| **PostgreSQL** | 16 | **15** (متوفر أكثر، API مستقر) |
| **Frontend UI** | shadcn/ui | **Tailwind CSS** (RTL native) |
| **Auth State** | كان مفقود | **JWT + BCrypt + Refresh tokens** (مكتمل) |
| **Modules** | 4 (Identity, Finance, Projects, Inventory) | **7** (إضافة Companies, Reports, Notifications) |
| **Architecture** | Modular Monolith (نظري) | **Modular Monolith (مطبق)** |
| **CI/CD** | GitHub Actions (مخطط) | **GitHub Actions (يعمل)** |
| **Phases** | 0 → 3 (مخطط) | **0 → 2.5+ (مكتمل)** |
| **Frontend** | غير مخطط | **8 صفحات متصلة بالـ API** |
| **DB Persistence** | في الـ sandbox | **محلياً على جهاز المطور** |

---

## 0️⃣ التحديث: Modules النهائية (7 Modules)

النظام يتكون من **7 Modules أساسية + 4 Shared Concerns**:

| Module | الوصف | الحالة | الـ PR |
|--------|-------|--------|------|
| **Identity** | Users, Tenants, RBAC, JWT, BCrypt | ✅ Phase 0 | #1 |
| **Companies** | Holding, Subsidiaries, Cost Centers | ✅ Phase 1.5 | #3 |
| **Finance** | CoA, Journal, Ledger, Posting Rules | ✅ Phase 1 | #2 |
| **Projects** | Project, Task, Resource, Budget | ✅ Phase 2.1 | #4 |
| **Inventory** | Item, Warehouse, Stock Movements | ✅ Phase 2.2-2.3 | #5-6 |
| **Reports** | Trial Balance, Stock Status, Project Profitability | ✅ Phase 2.5 | #8 |
| **Notifications** | In-app, Low-stock alerts | ✅ Phase 2.4 | #7 |
| **Shared/Events** | Event Bus + Outbox pattern | ✅ Phase 2.4 | #7 |
| **Shared/MultiTenancy** | Tenant isolation | ✅ Phase 0 | — |
| **Shared/Migrations** | FluentMigrator runner | ✅ Phase 0 | — |
| **Shared/Infrastructure** | DbConnectionFactory, BaseRepository | ✅ Phase 0 | — |

---

## 1️⃣ التقنيات النهائية (Production Stack)

### Backend ✅
- **C# / .NET 9** (محدّث من 8)
- **Dapper** + **FluentMigrator** (لا EF Core — قرار معتمد)
- **MartenDB** على PostgreSQL (Event Store)
- **Npgsql** + **PostgreSQL 15** (محدّث من 16)
- **Serilog** (structured logging)
- **FluentValidation** (request validation)
- **BCrypt.Net-Next** (password hashing)
- **Microsoft.AspNetCore.Authentication.JwtBearer**

### Frontend ✅
- **Next.js 14.2** + **React 18**
- **TypeScript 5.4** (strict mode)
- **Tailwind CSS 3.4** (RTL native)
- **Axios** (HTTP client + interceptors)
- **Zod** (schema validation)
- **React Hook Form** (form state)
- **TanStack Query 5** (data fetching)

### Infrastructure
- **PostgreSQL 15** (محدّث من 16 — أكثر استقراراً)
- **Redis** (cache, planned for Phase 3)
- **Docker** + **Docker Compose** (production)
- **Nginx** + **Let's Encrypt** (reverse proxy + SSL)
- **GitHub Actions** (CI/CD — يعمل)

### أدوات المطور ✅
- **GitFlow** (main + develop)
- **Conventional Commits** (feat, fix, docs, test, ci, chore)
- **DOX Documentation** (AGENTS.md hierarchy)
- **Mavis Cloud** (current sandbox)
- **Mavis Code** (local development)

---

## 2️⃣ المراحل المكتملة (Phase 0 → 2.5+)

### ✅ Phase 0: Foundation + Identity
**الـ PR #1** · `feature/phase-0-identity` → `main`
- Monorepo setup (Host, Shared, Modules)
- Identity Module (BCrypt, JWT, Refresh tokens)
- MultiTenancy (ITenantContext, TenantMiddleware)
- Infrastructure (DbConnectionFactory, BaseRepository)
- DOX documentation (AGENTS.md)

### ✅ Phase 1: Finance Core
**الـ PR #2** · `feature/phase-1-finance-core` → `main`
- Chart of Accounts
- Journal Entries + Double-Entry validation
- General Ledger + Trial Balance
- Posting Rules Engine

### ✅ Phase 1.5: Multi-Company Foundation
**الـ PR #3** · `feature/phase-1.5-multi-company` → `main`
- Companies + Cost Centers
- Holding Company hierarchy
- Expanded CoA seed

### ✅ Phase 2.1: Projects Module
**الـ PR #4** · `feature/phase-2.1-projects` → `main`
- Project, ProjectTask, Resource
- ProjectBudget + ResourceAssignment
- 20 unit tests

### ✅ Phase 2.2: Inventory Core
**الـ PR #5** · `feature/phase-2.2-inventory-core` → `main`
- Item, Warehouse, UoM, Category + default seed
- 16 unit tests

### ✅ Phase 2.3: Stock Movements
**الـ PR #6** · `feature/phase-2.3-stock-movements` → `main`
- StockMovement, StockLevel, StockReservation
- CQRS PostAsync
- LowStock notifications
- 13 unit tests

### ✅ Phase 2.4: Event Bus + Outbox
**الـ PR #7** · `feature/phase-2.4-event-bus` → `main`
- Postgres LISTEN/NOTIFY EventBus
- Outbox pattern (transactional safety)
- Finance event handlers (StockReceived/Issued)
- 8 unit tests
- 2 new events: StockTransferred, StockAdjusted

### ✅ Phase 2.5: Reports + Polish
**الـ PR #8** · `feature/phase-2.5-reports` → `main`
- 12 reporting endpoints (Finance, Inventory, Projects)
- 12 Report DTOs
- 3 Report Services

### ✅ Phase 2.5+: Reports Tests + Frontend Integration
**Commits** على `develop` (PR #9 مُلغى)
- 23 unit tests (Finance: 12, Inventory: 9, Project: 6)
- DTO computed properties hardening
- **Frontend API integration (8 pages)**
- DI fixes + CORS + Health route
- 7 commits جديدة على develop

---

## 3️⃣ المراحل المخططة (Phase 3+)

### 🔜 Phase 3: Procurement (الأسابيع القادمة)
**المدة المتوقعة:** 2-3 أسابيع
- Purchase Order (PO) aggregate
- Goods Receipt (GR) → Inventory integration
- Vendor Bill → Finance Journal Entry
- Payment workflow
- UI: Procurement pages

### 🔜 Phase 4: HR + Payroll
**المدة المتوقعة:** 3-4 أسابيع
- Employee entity + contracts
- Attendance + Leave management
- Payroll calculation engine
- Payslip generation
- Tax deductions

### 🔜 Phase 5: Mobile App
**المدة المتوقعة:** 2-3 أسابيع
- React Native + Expo
- Shared API
- Offline-first
- Push notifications

### 🔜 Phase 6: Advanced Reporting
**المدة المتوقعة:** 2 أسابيع
- Drag-drop report builder
- Export to PDF/Excel
- Scheduled reports
- Email delivery

---

## 4️⃣ البنية المعمارية النهائية

```
┌──────────────────────────────────────────────────────────────┐
│  Frontend (Next.js 14 + Tailwind + RTL)                      │
│  localhost:3000                                              │
└─────────────────────────┬────────────────────────────────────┘
                          │ HTTPS + JWT
                          ▼
┌──────────────────────────────────────────────────────────────┐
│  API Host (ASP.NET Core 9)                                   │
│  localhost:5000                                              │
│  ─ CORS: SetIsOriginAllowed(_ => true)                       │
│  ─ Health: /health/live                                      │
│  ─ Swagger: /swagger                                         │
└────────┬─────────────────────────────────┬───────────────────┘
         │                                 │
         │ Dapper                          │ Npgsql LISTEN/NOTIFY
         │                                 │
         ▼                                 ▼
┌────────────────────────┐    ┌─────────────────────────────────┐
│  erp_system (PG 15)   │    │  erp_events (PG 15)             │
│  localhost:5432       │    │  localhost:5432                 │
│  user: erp_user       │    │  user: erp_user                 │
│  pass: erp_password   │    │  pass: erp_password             │
│  ─ identity.*         │    │  ─ outbox_events                │
│  ─ finance.*          │    │  ─ processed_events             │
│  ─ projects.*         │    │  ─ event_stream (Marten)        │
│  ─ inventory.*        │    └─────────────────────────────────┘
│  ─ companies.*        │
│  ─ reports.*          │
│  ─ notifications.*    │
└────────────────────────┘
```

---

## 5️⃣ سير العمل (Workflow)

### GitFlow
- **`main`**: Production (releases فقط)
- **`develop`**: Integration (default branch)
- **`feature/*`**: ميزات جديدة
- **`hotfix/*`**: إصلاحات طارئة
- **`release/*`**: تحضير release

### Conventional Commits
```
<type>(<scope>): <description>

Types: feat, fix, docs, test, ci, chore, refactor, perf
Scope: frontend, backend, identity, finance, etc.
```

### PR Workflow (محدّث)

**القاعدة الجديدة:** الوكيل المحلي (Mavis Code) يقوم بفتح PR فوراً بعد كل commit.

```bash
# بعد كل commit:
git push origin feature/new-feature
# Mavis Code يفتح PR تلقائياً:
# - head: feature/new-feature
# - base: develop
# - title: <commit message>
# - body: <commit description>
# - labels: feat/fix/docs/test
# - assignees: anas600
# - reviewers: anas600
```

**Code Review Process:**
- ✅ **الوكيل يفتح الـ PR**
- ✅ **anas600 يراجع الكود بنفسه** (self-review)
- ✅ **CI يمر** (lint + build + tests)
- ✅ **Squash merge** إلى develop
- ✅ **Auto-delete branch** بعد الدمج
- ✅ **develop → main** عند الـ release

---

## 6️⃣ قرارات معتمدة (محدّثة)

### معمارية
1. ✅ **Dapper + FluentMigrator + MartenDB** (بدل EF Core)
2. ✅ **Next.js 14 + Tailwind** (بدل shadcn/ui — أبسط للـ RTL)
3. ✅ **Event-Driven Architecture** بين الـ Modules
4. ✅ **Modular Monolith** → قابل للتحويل لـ Microservices
5. ✅ **PostgreSQL 15** (بدل 16 — أكثر استقراراً)

### أمان
6. ✅ **JWT + BCrypt + Refresh tokens** (60min + 14days)
7. ✅ **tenant_id في كل صف** (Repository pattern)
8. ✅ **DTO computed properties** (read-only)
9. ✅ **FluentValidation** على Create operations

### Git/Workflow
10. ✅ **GitFlow** (main + develop)
11. ✅ **Conventional Commits** (1-3 files per commit)
12. ✅ **DOX AGENTS.md** (hierarchical)
13. ✅ **Auto-delete branches** بعد merge

### أدوات المطور
14. ✅ **Mavis Code** (local agent)
15. ✅ **Mavis Cloud** (sandbox agent)
16. ✅ **GitHub CLI** (للـ PR management)
17. ✅ **GitHub Actions** (CI/CD)

---

## 7️⃣ بيانات الاعتماد (Credentials)

### Database (PostgreSQL 15)
```
Host:     localhost
Port:     5432
User:     erp_user
Password: erp_password
DB 1:     erp_system (OLTP)
DB 2:     erp_events (Event Store)
```

### Application
```
Backend:  http://localhost:5000
Frontend: http://localhost:3000
Swagger:  http://localhost:5000/swagger
```

### Environment
```
ASPNETCORE_ENVIRONMENT = Development
```

---

## 8️⃣ الإحصائيات الحالية (Statistics)

| المقياس | القيمة |
|--------|--------|
| **Commits الإجمالي** | 71 |
| **PRs المدمجة** | 8 (Phase 0 → 2.5) |
| **Modules** | 7 Backend + 1 Frontend |
| **صفحات Frontend** | 8 |
| **API Endpoints** | 50+ |
| **DB Tables** | ~25 (4 schemas) |
| **Unit Tests** | 23 passing + 10 marked Skip |
| **CI Workflows** | 1 (ci.yml) |
| **AGENTS.md files** | 11 (hierarchical) |
| **Public URLs شغّالة** | 2 (Frontend + API) |
| **حسابات تجريبية** | 4 (u1@c.com, new@test.com, إلخ) |

---

## 9️⃣ الـ Roadmap المختصر

| Phase | الوصف | الحالة | الـ PR |
|-------|------|--------|------|
| 0 | Foundation + Identity | ✅ | #1 |
| 1 | Finance Core | ✅ | #2 |
| 1.5 | Multi-Company | ✅ | #3 |
| 2.1 | Projects | ✅ | #4 |
| 2.2 | Inventory Core | ✅ | #5 |
| 2.3 | Stock Movements | ✅ | #6 |
| 2.4 | Event Bus | ✅ | #7 |
| 2.5 | Reports | ✅ | #8 |
| 2.5+ | Tests + Frontend | ✅ | direct on develop |
| **3** | **Procurement** | 🔜 | — |
| 4 | HR + Payroll | 🔜 | — |
| 5 | Mobile App | 🔜 | — |
| 6 | Advanced Reporting | 🔜 | — |

---

## 🔟 ملاحظات ختامية

### للإنتاج (Production Checklist)
- [ ] VPS ثابت (DigitalOcean / Hetzner)
- [ ] Domain + Let's Encrypt
- [ ] Docker + Docker Compose
- [ ] Nginx + Rate Limiting
- [ ] Database backups (يومية + PITR)
- [ ] Monitoring (Prometheus + Grafana)
- [ ] Centralized logging (ELK / Datadog)
- [ ] Secrets في Vault
- [ ] HTTPS-only + HSTS
- [ ] CDN (Cloudflare)

### ملاحظات
- **الـ sandbox يختلف عن الإنتاج**: في الـ sandbox كل شيء ephemeral. للإنتاج: VPS ثابت.
- **Tunnels مؤقتة**: serveo.net يولّد URLs عشوائية. للإنتاج: domain ثابت.
- **PR workflow محدّث**: الوكيل المحلي (Mavis Code) يفتح الـ PR، الـ user يراجع ويعتمد.

---

**آخر تحديث:** 2026-06-16
**الإصدار التالي:** v2.1 (بعد Phase 3)
