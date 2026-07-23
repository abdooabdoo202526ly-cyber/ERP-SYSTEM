# 💰 Payroll Module — AGENTS.md

> **Phase 4** — Payroll + End of Service (EOS) calculation for Libya.
> يتعامل مع: الرواتب الشهرية، الضرائب الليبية التصاعدية، التأمينات الاجتماعية، مكافأة نهاية الخدمة.

---

## 📦 شو فيه

```
Payroll/
├── Application/
│   ├── Dtos.cs                      # PayrollRunDto, PayrollItemDto, PayslipDto, EosPreviewDto, CreatePayrollRunRequest
│   ├── Validators.cs                # CreatePayrollRunRequestValidator
│   └── Services/
│       ├── PayrollService.cs        # Create → Process → Post workflow + GL posting
│       └── EosService.cs            # EOS calculation + preview
├── Domain/
│   ├── Calculators/
│   │   ├── LibyaTaxCalculator.cs        # 5% (0-1000) + 10% (1000-5000) + 10% (>5000) progressive tax
│   │   ├── EosCalculator.cs             # EOS formula: ≤5y: salary*years; >5y: salary*5 + 2*salary*(y-5)
│   │   └── SocialInsuranceCalculator.cs # 3.75% employee + 7.5% employer
│   └── Entities/
│       ├── SalaryStructure.cs       # per-employee base/allowances/deductions
│       ├── PayrollRun.cs            # Draft → Processed → Posted
│       └── PayrollItem.cs           # per-employee calculation result
└── Infrastructure/
    ├── IPayrollRepository.cs        # CRUD + queries
    └── PayrollRepository.cs         # Dapper implementation
```

## 🌐 HTTP Endpoints (في `Host/Controllers/HrController.cs`)

| Method | Route | الغرض | Auth |
|--------|-------|------|------|
| GET | `/api/hr/payroll/runs` | قائمة كل الـ runs (مع pagination) | Required |
| GET | `/api/hr/payroll/runs/{id}` | تفاصيل run واحد | Required |
| POST | `/api/hr/payroll/runs` | إنشاء run جديد (Draft) | Required |
| POST | `/api/hr/payroll/runs/{id}/process` | حساب كل البنود (Transition to Processed) | Required |
| POST | `/api/hr/payroll/runs/{id}/post` | ترحيل للـ GL (Transition to Posted) | Required |
| GET | `/api/hr/payroll/runs/{id}/items` | بنود الـ run لكل الموظفين | Required |
| GET | `/api/hr/payroll/runs/{id}/items/{empId}/payslip` | قسيمة راتب موظف | Required |
| GET | `/api/hr/payroll/eos/{empId}` | معاينة مكافأة نهاية الخدمة | Required |

## 🗄️ Database Schema (5 tables)

Migration: `20260624_100000_CreatePayrollTables.cs`

| Table | الغرض | Indexes |
|-------|------|---------|
| `salary_structures` | تكوين الراتب لكل موظف (base + allowances + deductions) | `(tenant_id, employee_id) UNIQUE` |
| `payroll_runs` | دورة الرواتب الشهرية (period, status, totals) | `(tenant_id, period_start)`, `(status)` |
| `payroll_items` | حساب كل موظف في run معين | `(tenant_id, run_id)`, `(employee_id)` |
| `payslip_components` | مكونات الراتب (basic, housing, transport, overtime, tax, social_ins) | `(tenant_id, item_id)` |
| `employee_eos_balance` | رصيد EOS المتراكم لكل موظف | `(tenant_id, employee_id) UNIQUE` |

كل الجداول تحتوي `tenant_id` + `created_at` + `updated_at` (multi-tenancy + audit).

## 🧮 الـ Calculators (Libya-specific)

### LibyaTaxCalculator (ضرائب ليبيا)
تصاعدية على الراتب الإجمالي (gross):
- **0 - 1,000 LYD**: 0%
- **1,000 - 5,000 LYD**: 5% على المبلغ الزائد عن 1,000
- **5,000+ LYD**: 10% على المبلغ الزائد عن 5,000 (بعد الـ 5% على الـ 4,000 الأولى)

مثال: راتب 6,000 LYD:
- أول 1,000: 0
- من 1,000 لـ 5,000 (4,000 × 5%): 200
- من 5,000 لـ 6,000 (1,000 × 10%): 100
- **مجموع الضريبة: 300 LYD**

### EosCalculator (مكافأة نهاية الخدمة)
- ≤ 5 سنوات خدمة: `salary × years`
- > 5 سنوات: `salary × 5 + salary × 2 × (years - 5)`

مثال: 8 سنوات × 1,500 LYD:
- 1,500 × 5 = 7,500
- 1,500 × 2 × 3 = 9,000
- **مجموع EOS: 16,500 LYD**

### SocialInsuranceCalculator (التأمينات)
- **موظف**: 3.75% من الراتب الأساسي
- **صاحب العمل**: 7.5% من الراتب الأساسي (يذهب للمصروف، ليس للـ employee)

## 🔄 الـ Workflow (Payroll Run lifecycle)

```
[Create] → Status: Draft (no items yet)
   ↓
[Process] → يحسب كل الموظفين، Status: Processed (read-only)
   ↓
[Post] → يرحل للـ GL (Journal Entry + Ledger posting)، Status: Posted
   ↓
[Lock] → لا يمكن التعديل بعد Post (للـ audit)
```

**حالة الـ enum:** `Draft | Processed | Posted | Voided`

## 🏦 GL Posting (Default CoA)

عند الـ `Post`:
- **Debit 4200 G&A Expenses** (Salary + Social Insurance Employer)
- **Credit 1210 Cash** (Net Pay + Employee Tax + Social Insurance Employee)

**Smart fallback pattern:**
```csharp
var expenseAccount = await coaService.GetByCodeAsync(tenantId, "4200", ct)
    ?? await coaService.GetByCodeAsync(tenantId, "5500", ct);  // fallback
```

هذا يسمح للـ tenants بـ custom CoA (مثل 5500) بدون كسر.

## 📋 الـ DTOs

```csharp
// Input
CreatePayrollRunRequest {
    periodStart: DateTime,  // camelCase
    periodEnd: DateTime,
    notes: string?
}

// Output
PayrollRunDto {
    id, tenantId, periodStart, periodEnd, status, totalGross,
    totalNet, totalTax, totalSocialIns, itemCount, createdAt, ...
}

PayslipDto {
    employeeId, employeeName, baseSalary, allowances: Component[],
    deductions: Component[], grossPay, taxAmount, socialInsEmployee,
    netPay, eosBalanceAccrued
}
```

## ✅ Validation Rules (FluentValidation)

- `periodEnd > periodStart` (تاريخ نهاية بعد بداية)
- لا يوجد run آخر متبادل في نفس الفترة
- مبلغ الرواتب يجب أن يكون موجب
- لا يمكن تشغيل `Process` على run ليس Draft
- لا يمكن تشغيل `Post` على run ليس Processed

## 🐛 Known Issues & Fixes

### 1. EnumStringTypeHandler (مهم)
**المشكلة:** Dapper لا يحوّل string → enum تلقائياً.
**الحل:** `EnumStringTypeHandler<TEnum>` مسجّل في `Program.cs` لكل الـ enums:
- PayrollRunStatus
- PayrollItemStatus
- LeaveStatus, PurchaseOrderStatus, GoodsReceiptStatus, VendorBillStatus

### 2. SQL SELECT bug (مهم جداً)
**المشكلة:** في `PayrollRepository.GetItemsByRunAsync` كان مفقود `SELECT` keyword.
**الإصلاح:** `var sql = $"SELECT {ItemSel} FROM payroll_items WHERE ...";`

### 3. Redis 5s timeout (Dev فقط)
**المشكلة:** `StackExchange.Redis.PingAsync` كان يحجب 5s.
**الإصلاح:** في `Program.cs`: `ConnectTimeout=1000ms, SyncTimeout=500ms, AsyncTimeout=500ms`.
وفي `HealthController`: cap عند 500ms عبر `CancellationTokenSource`.

## 🔗 مرتبطة بـ

- [`../AGENTS.md`](../AGENTS.md) — Backend overview
- [`../../AGENTS.md`](../../AGENTS.md) — Root
- [`../HR/AGENTS.md`](../HR/AGENTS.md) — مصدر الـ Employees + Departments
- [`../Finance/AGENTS.md`](../Finance/AGENTS.md) — CoA + JournalEntries + PostingRules
- [Migration 010](../Shared/Migrations/20260624_100000_CreatePayrollTables.cs)
- [`../../../frontend/AGENTS.md`](../../../frontend/AGENTS.md) — Frontend pages


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
