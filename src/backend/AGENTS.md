# ⚙️ src/backend/AGENTS.md

> الـ Backend (ASP.NET Core 9 + C#).

## شو فيه

```
backend/
├── Host/                  # نقطة الدخول: Program.cs, Controllers, Swagger
├── Modules/               # Modular Monolith — كل module مستقل
│   ├── Identity/          # ✅ Phase 0 (مكتمل)
│   ├── Finance/           # ✅ Phase 1 (مكتمل)
│   ├── Companies/         # ✅ Phase 1.5 (مكتمل)
│   ├── Projects/          # ✅ Phase 2.1 (مكتمل)
│   ├── Inventory/         # ✅ Phase 2.2-2.3 (مكتمل)
│   ├── Reports/           # ✅ Phase 2.5 (مكتمل)
│   ├── Notifications/     # ✅ Phase 2.4 (مكتمل)
│   ├── Procurement/       # ✅ Phase 3 (مكتمل — Vendor + PO + GR + Bill)
│   ├── HR/                # ✅ Phase 3.5 (مكتمل — Department + Employee + Attendance + Leave)
│   ├── Payroll/           # ✅ Phase 4 (مكتمل — SalaryStructure + PayrollRun + Libya Tax + EOS)
│   ├── Payments/          # ✅ Phase 5 Sprint 2 (مُخطط — AP + GL Reports + Aging AP)
│   └── AccountsReceivable/ # ✅ Phase 5 Sprint 1 (مكتمل — Customer + SalesInvoice + Receipt + Aging)
├── Shared/                # كود مشترك بين الموديولات
│   ├── Infrastructure/    # DbConnectionFactory
│   ├── MultiTenancy/      # TenantContext + Middleware
│   ├── Migrations/        # FluentMigrator migrations
│   └── Events/            # Event contracts
├── Tests/                 # xUnit test projects
└── Dockerfile             # multi-stage build للـ api
```

## Conventions / القواعد

### Modular Monolith Pattern

- كل module ينقسم إلى 3 طبقات:
  - `Entities/` — POCO classes للجداول
  - `Application/` — DTOs, Validators, Services, Use cases
  - `Infrastructure/` — Repositories (Dapper queries)
- الـ Host يضمّ كل الموديولات عبر `<Compile Include="..\Modules\**\*.cs" />`
  - **السبب**: مرحلة Phase 0. لاحقاً (عند استقرار الـ boundaries) نحوّلها إلى csproj مستقل
- لا يوجد access بين الـ modules إلا عبر:
  - `Shared/Events` (Pub/Sub)
  - `Shared/MultiTenancy` (Tenant context)
  - Direct interface call (للحالات النادرة، موثّقة)

### Dapper + FluentMigrator + MartenDB

- **لا EF Core** — القرار معتمد في PLAN.md
- **Dapper** للـ OLTP queries — يدوي لكن مرن
- **FluentMigrator** للـ schema — كل migration يرقم بـ timestamp
- **MartenDB** للـ Event Store — schema منفصل `mt_events` (قاعدة منفصلة حالياً)
- **استخدم snake_case** في SQL و الـ DTO mappings (`AsName()` في Dapper)

### Multi-tenancy

- كل entity يجب أن يحتوي `TenantId`
- الـ `TenantContext` يحوي `TenantId` و `UserId` للـ request الحالي
- الـ `TenantMiddleware` يلتقطها من JWT claims
- **قاعدة لاحقة**: أي repository query يفلتر بـ `tenant_id` (Audit log)

### Auth

- `JwtTokenService` — singleton
- `IAuthService` — scoped (للـ DB context)
- BCrypt workFactor = 12
- Access Token: 60min، Refresh: 14 يوم
- **Token rotation إلزامي** — أي refresh يُلغي القديم
- **Reuse detection**: استخدام refresh ملغى = هجوم → نُلغي كل جلسات المستخدم

## لما تشتغل هنا

1. اقرأ الـ AGENTS.md للـ module اللي بتشتغل فيه
2. الـ pattern الجديد يتبع:
   - **Entity** جديد → migration جديدة → repository → service → controller → test
3. لكل feature جديد: حدّث AGENTS.md المعني
4. الـ validation في `Application/*/Validators.cs` (FluentValidation)

## بعد التعديل

- شغّل `dotnet build` ثم `dotnet test`
- إذا أضفت endpoint جديد، حدّث Swagger XML comments
- إذا أضفت migration جديدة، لا تعدّل القديم — أنشئ جديد دائماً

## مرتبطة بـ

- [`../AGENTS.md`](../AGENTS.md)
- [`Host/AGENTS.md`](Host/AGENTS.md)
- [`Modules/Identity/AGENTS.md`](Modules/Identity/AGENTS.md)
- [`Modules/Finance/AGENTS.md`](Modules/Finance/AGENTS.md)
- [`Modules/Companies/AGENTS.md`](Modules/Companies/AGENTS.md)
- [`Modules/Projects/AGENTS.md`](Modules/Projects/AGENTS.md)
- [`Modules/Inventory/AGENTS.md`](Modules/Inventory/AGENTS.md)
- [`Modules/Reports/AGENTS.md`](Modules/Reports/AGENTS.md)
- [`Modules/Notifications/AGENTS.md`](Modules/Notifications/AGENTS.md)
- [`Modules/Procurement/AGENTS.md`](Modules/Procurement/AGENTS.md) 🆕 Phase 3
- [`Modules/HR/AGENTS.md`](Modules/HR/AGENTS.md) 🆕 Phase 3.5
- [`Modules/Payroll/AGENTS.md`](Modules/Payroll/AGENTS.md) 🆕 Phase 4
- [`Modules/AccountsReceivable/AGENTS.md`](Modules/AccountsReceivable/AGENTS.md) 🆕 Phase 5 Sprint 1
- [`Shared/AGENTS.md`](Shared/AGENTS.md)
- [`Tests/AGENTS.md`](Tests/AGENTS.md)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
