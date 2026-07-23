# 🏢 src/backend/Modules/Companies/AGENTS.md

> Companies Module — ✅ Phase 1.5 (مكتمل — Holding + Subsidiaries + CostCenters).
>
> محدّث: 2026-07-22 — P1a architectural cleanup (notifications route + Procurement/Payments class-level `[Route]`).

## شو فيه

```
Companies/
├── Entities/
│   ├── Company.cs        # Company (Holding/Subsidiary hierarchy)
│   └── CostCenter.cs     # CostCenter + CostCenterType enum
├── Application/
│   └── Services/
│       ├── CompanyService.cs        # ICompanyService + Result pattern + ITenantBootstrap
│       └── CostCenterService.cs     # ICostCenterService + CreateCostCenterRequest + BudgetStatus DTO
└── Infrastructure/
    ├── IRepositories.cs             # ICompanyRepository + ICostCenterRepository
    ├── CompanyRepository.cs         # Dapper impl (snake_case mapping)
    └── CostCenterRepository.cs      # Dapper impl (snake_case mapping)
```

## Domain Model

### Company
- هرمي عبر `parent_company_id` (self-FK، nullable) — **Holding → Subsidiary** tree
- نوعان: `is_group = true` (Holding فقط) أو `false` (Subsidiary)
- `code` فريد لكل tenant (case-insensitive)
- `base_currency` (ISO 4217) — تنتقل من الـ Holding للـ Subsidiaries عند إنشائها
- `is_active` للـ soft-delete (تظهر في الـ queries فقط عندما `includeInactive=true`)
- حقول `CreatedAt` / `UpdatedAt` (UTC) تُحدّث في الـ Insert / Update

### CostCenter
- ينتمي لشركة عبر `company_id` (nullable — CostCenter على مستوى tenant ممكن)
- `type` enum: `Project / Department / Branch / ProductLine / Activity / Other`
- هرمي اختياري عبر `parent_id` (self-FK، nullable)
- `budget_amount` (Decimal(18,4)، nullable) — يُستخدم مع `BudgetStatus` لمتابعة الصرف
- حقول `sku` / `location` / `activity_category` للسياق الإضافي
- `start_date` / `end_date` (nullable) — صلاحية زمنية اختيارية

## Application Services

### ICompanyService (`CompanyService.cs`)
| Method | الوصف |
|--------|-------|
| `CreateHoldingAsync` | إنشاء Holding جديد + تهيئة CoA الافتراضي (Finance) + seed UoMs/Categories (Inventory) |
| `AddSubsidiaryAsync` | إنشاء Subsidiary تحت Holding + نسخ CoA من الـ Holding |
| `GetByIdAsync` | جلب شركة بالمعرّف (مع تحقق tenant) |
| `ListAsync` | قائمة كل الشركات (pagination-friendly) |
| `GetSubsidiariesAsync` | الـ subsidiaries المباشرة لـ Holding معيّن |
| `GetTreeAsync` | شجرة كاملة Holding → Subsidiaries |
| `DeactivateAsync` | soft-delete (`is_active = false`) |

**`ITenantBootstrap` integration** — `CompanyService.OnTenantCreatedAsync` يُستدعى عند إنشاء tenant جديد ويضمن وجود Holding واحد (كود `000`).

**`CompanyResult<T>` + `CompanyErrorCode`** — `NotFound / AlreadyExists / ValidationError / InUse / Internal`.

### ICostCenterService (`CostCenterService.cs`)
| Method | الوصف |
|--------|-------|
| `CreateAsync` | إنشاء CostCenter جديد (يتحقق من تفرّد الكود) |
| `GetByIdAsync` | جلب بالمعرّف (مع تحقق tenant) |
| `ListAsync` | قائمة مع فلاتر (companyId, type, includeInactive) |
| `GetChildrenAsync` | الأبناء المباشرون لـ CostCenter معيّن |
| `GetBudgetStatusAsync` | `BudgetAmount / SpentAmount / Remaining / Utilization%` |
| `DeactivateAsync` | soft-delete |

**`CostCenterResult<T>` + `CostCenterErrorCode`** — `NotFound / AlreadyExists / ValidationError / Internal`.

## Infrastructure Repositories

- `ICompanyRepository` / `CompanyRepository` — Dapper queries على `companies`
- `ICostCenterRepository` / `CostCenterRepository` — Dapper queries على `cost_centers`
- كل الـ queries تفلتر بـ `tenant_id` (multi-tenancy enforcement)
- `SelectColumns` const موحّد لكل repo (يقلّل تكرار الـ SQL)
- soft-delete = `is_active = false` (لا DELETE حقيقي)

## Controllers + Routes

### CompaniesController
| Method | Path | الوصف |
|--------|------|-------|
| GET | `/api/companies` | قائمة الشركات (cache 15min) |
| GET | `/api/companies/tree` | شجرة كاملة (cache 15min) |
| GET | `/api/companies/{id}` | تفاصيل شركة (cache 15min) |
| GET | `/api/companies/{id}/subsidiaries` | الشركات الفرعية |
| POST | `/api/companies/holding` | إنشاء Holding |
| POST | `/api/companies/subsidiary` | إنشاء Subsidiary |
| DELETE | `/api/companies/{id}` | deactivate (soft-delete) |

### CostCentersController
| Method | Path | الوصف |
|--------|------|-------|
| GET | `/api/cost-centers` | قائمة (filters: companyId, type, includeInactive) |
| GET | `/api/cost-centers/{id}` | تفاصيل |
| GET | `/api/cost-centers/{id}/children` | الأبناء المباشرون |
| GET | `/api/cost-centers/{id}/budget-status` | حالة الميزانية (asOf) |
| POST | `/api/cost-centers` | إنشاء |
| DELETE | `/api/cost-centers/{id}` | deactivate |

**Caching**: `ITenantCache` بـ TTL = 15min، invalidated عند أي write.

## Frontend Pages

> لم تُنشأ صفحات Companies / CostCenters في الـ frontend بعد — الـ admin/ يحتوي حالياً:
> `item-categories/`, `notifications/`, `posting-rules/`.
> عند إضافة UI: أنشئ `src/frontend/app/(authenticated)/admin/companies/` و `cost-centers/`.

## ملاحظات معمارية

- **Holding bootstrapping**: عند إنشاء tenant جديد، يُستدعى `OnTenantCreatedAsync` تلقائياً عبر `ITenantBootstrap`. هذا يضمن وجود Holding واحد + CoA افتراضي + UoMs/Categories قبل أي عملية مالية/مخزنية.
- **CoA cloning**: عند إضافة Subsidiary، يُستدعى `IAccountRepository.CloneCoAFromCompanyAsync` لنسخ شجرة الحسابات من الـ Holding. الـ Subsidiary تحصل على نسخة مستقلة (multi-company accounting).
- **Cross-module call**: `CompanyService` يستهلك مباشرة `IAccountRepository` (Finance) و `IInventoryBootstrapper` (Inventory). هذا استثناء موثّق — غالبية التواصل عبر `Shared/Events` لكن الـ bootstrap synchronous.
- **Cache invalidation**: عند أي create/deactivate للشركات، يُمسح tenant cache بالكامل (لأن الـ tree + lists يتأثرون).
- **No real delete**: `is_active = false` فقط. الـ auditability أهم من الـ storage savings.
- **P1a routing cleanup**: Controllers تتبع النمط الموحّد `[Route("api/{module}")]` على مستوى الـ class + relative `[HttpGet("...")]` على الـ methods. هذا يطبق على Companies (كان موحّداً) + CostCenters (كان موحّداً) — التوحيد في هذه الجولة طال Procurement + Payments + Notifications.

## لما تشتغل هنا

- **إضافة نوع CostCenter جديد**: عدّل `CostCenterType` enum + حدّث UI (لاحقاً) + حدّث `BudgetStatus` لو احتاج فلتر
- **إضافة company hierarchy levels (e.g., Division)**: عدّل `Company` + أضف migration جديدة + Controllers
- **Inter-company transactions**: أنشئ `InterCompanyService` يستخدم `IJournalEntryService` (Finance) + `CompanyService` لتوليد قيود مزدوجة
- **UI للشركات**: أضف `admin/companies/page.tsx` + `admin/companies/[id]/page.tsx` (tree view + جدول)
- **Budget enforcement**: ربط `SpentAmount` في `BudgetStatus` مع `JournalEntry` (عند ترحيل قيد على cost-center)

## بعد التعديل

- شغّل `dotnet build` → 0 errors
- الـ migrations الخاصة بالـ Companies موجودة في `Shared/Migrations/`
- اختبر بـ `curl`: POST `/api/companies/holding` → GET `/api/companies` → GET `/api/companies/tree`
- لو غيّرت الـ entities: migration جديدة (لا تعدّل قديمة) + update DTOs

## تكامل مع الموديولات الأخرى

- **Identity**: كل Holding مرتبط بـ `TenantId` (1:1) — لا User-level هنا
- **Finance**: Holding يمتلك `AccountRepository` CoA خاص؛ Subsidiaries تنسخ الـ CoA عند الإنشاء
- **Inventory**: Holding هو الـ root للـ warehouses (لاحقاً)
- **Procurement / HR / AR / AP**: كلها تربط الشركات عبر `company_id` (multi-company filtering)
- **Notifications**: لم يُربط بعد — مستقبلي عند تغيير Tenant plan أو إضافة subsidiary

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md) — Backend conventions
- [`../Finance/AGENTS.md`](../Finance/AGENTS.md) — CoA ownership + cloning
- [`../Inventory/AGENTS.md`](../Inventory/AGENTS.md) — UoMs/Categories seed
- [`../Identity/AGENTS.md`](../Identity/AGENTS.md) — Tenant bootstrapping
- [`../../Host/AGENTS.md`](../../Host/AGENTS.md) — DI registration
- [`../../Shared/AGENTS.md`](../../Shared/AGENTS.md) — Migrations

---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
