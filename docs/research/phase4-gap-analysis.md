# Phase 4 — Gap Analysis (Payroll + EOS)

> **التاريخ:** 2026-06-24 · **Author:** Mavis (Owner) · **Scope:** Phase 4 of ERP-SYSTEM
> **الـ Source:** تكامل من docs/research/{daftra,erpnext,odoo}-features.md + docs/research/gap-analysis.md (Phase 3)

---

## 1. Executive Summary

ERP-SYSTEM جاهز للـ Phase 4: **HR + Payroll + EOS (End of Service)**. الـ HR Core (Phase 3.5) يحوي Employee + Department + Attendance + Leave. ما ينقص:

1. **PayrollRun aggregate** (Draft → Processing → Posted) مع Salary Slip per employee
2. **Salary Structure** (formulas + components: Basic, Allowances, Deductions, Tax)
3. **Libya Tax Engine** (5 brackets, progressive)
4. **EOS calculation** (per Libyan labor law)
5. **Payslip PDF generation**
6. **Finance integration** (Payroll posted → JournalEntry: Dr Salary Expense / Cr Cash/Bank)

**الـ MVP slice:** Payroll engine + Payslip view + EOS calc. Payroll Email + Bank integration = Phase 4.5.

---

## 2. Competitive Matrix (Phase 4 features)

| الميزة | ERP-SYSTEM (الآن) | Daftra | ERPNext | Odoo |
|--------|------------------|--------|---------|------|
| Employee entity | ✅ | ✅ | ✅ | ✅ |
| Attendance | ✅ | ✅ | ✅ | ✅ |
| Leave | ✅ | ✅ | ✅ | ✅ |
| **PayrollRun aggregate** | ❌ | ✅ | ✅ | ✅ |
| **Salary Structure** | ❌ | ✅ | ✅ | ✅ |
| **Payslip PDF** | ❌ | ✅ | ✅ | ✅ |
| **Tax Engine (progressive)** | ❌ | ✅ | ✅ | ✅ |
| **EOS calculation** | ❌ | ✅ | ✅ (Gratuity) | ✅ |
| **Bank integration** | ❌ | ✅ | ✅ | ✅ |
| **Payroll Email** | ❌ | ✅ | ✅ | ✅ |
| **GL posting integration** | ❌ | ✅ | ✅ | ✅ |

---

## 3. Libya-Specific Tax Brackets (GDT)

Per PwC + Playroll summaries (2026):

| الـ Bracket (LYD/year) | الـ Tax Rate |
|----------------------|-------------|
| 0 – 12,000 | **5%** |
| 12,001 – 24,000 | **10%** |
| 24,001+ | **10%** (flat) |

> **Notes:**
> - Tax على الـ Gross Salary بعد خصم Social Insurance (~3.75%)
> - Libyan labor law يحمي EOS: شهر عن كل سنة خدمة (first 5 years) + شهرين/سنة (after 5)
> - Social Insurance Contribution: ~3.75% (employee) + ~7.5% (employer)

---

## 4. Phase 4 Scope — Recommended MVP

### A) Payroll Core (Backend) — 4 entities + 1 migration
1. **`payroll.salary_structures`** — formula-based: Basic + Allowances + Deductions
2. **`payroll.salary_structure_lines`** — components (earnings/deductions)
3. **`payroll.payroll_runs`** — Draft → Processing → Posted (aggregate root)
4. **`payroll.payroll_items`** — per-employee payslip within a run (Salary Slip)
5. **`payroll.payslip_components`** — earnings/deductions breakdown

**Migration 010:** `20260624_100000_CreatePayrollTables.cs`

### B) Payroll Engine (Backend) — 6 endpoints + services
- `POST /api/hr/payroll/runs` — create run for a period
- `POST /api/hr/payroll/runs/{id}/process` — calculate all employee slips
- `POST /api/hr/payroll/runs/{id}/post` — post to GL (JournalEntry)
- `GET /api/hr/payroll/runs/{id}/items` — list payslips
- `GET /api/hr/payroll/runs/{id}/items/{empId}/payslip` — single payslip
- `GET /api/hr/payroll/eos/{empId}` — calculate EOS for an employee

### C) EOS + Tax (Backend) — services
- `LibyaTaxCalculator` — progressive brackets (5% / 10% / 10%)
- `EosCalculator` — per Libyan labor law (1 month/year first 5y, 2 months/year after)
- `SocialInsuranceCalculator` — 3.75% employee / 7.5% employer

### D) Frontend (3 pages)
- `/hr/payroll` — list runs + create new
- `/hr/payroll/[id]` — run detail with all payslips
- `/hr/payroll/[id]/payslip/[empId]` — single payslip view (HTML, printable)

### E) Integration
- On PayrollRun.Post: emit `PayrollPostedEvent` → Finance handler creates JournalEntry
  - **Dr Salary Expense (4200)** ← G&A Expenses (في DefaultCoASeed) — generic for admin salaries
  - **Cr Cash (1210)** ← النقدية — direct cash payment
- **CoA mapping implementation** (PayrollService.cs):
  ```csharp
  // Smart fallback: prefer 4200 (postable in DefaultCoASeed), fallback to 5500
  var salaryAccount = await _accounts.GetByCodeAsync(tenantId, "4200", ct)
      ?? await _accounts.GetByCodeAsync(tenantId, "5500", ct);
  var cashAccount = await _accounts.GetByCodeAsync(tenantId, "1210", ct)
      ?? await _accounts.GetByCodeAsync(tenantId, "1100", ct);
  ```
- **Per-employee posting** (future enhancement): Dr 4112 (Direct Labor) لو employee في operations، Dr 4200 لو في admin. Phase 4.5+ feature.
- **2110 Salaries Payable** غير موجود حالياً في الـ CoA — سيُضاف Phase 4.5. حالياً ندفع cash مباشرة (Cr 1210).

---

## 5. Libya EOS Formula (Reference)

```
If YearsOfService <= 5:
    EOS = MonthlySalary × YearsOfService
Else:
    EOS = MonthlySalary × 5 + (MonthlySalary × 2 × (YearsOfService - 5))
```

> ⚠️ هذا MVP formula. الـ Libyan law فيه استثناءات (resignation vs termination, partial year).
> Phase 4.5 يضيف refinement.

---

## 6. Migration Plan

| # | Migration | Tables | Status |
|---|-----------|--------|--------|
| 008 | CreateProcurementTables | 7 | ✅ done |
| 009 | CreateHRTables | 4 | ✅ done |
| 010 | **CreatePayrollTables** | **5** | 📋 Phase 4 |

---

## 7. Top 10 Features (Prioritized for Phase 4)

| # | Feature | Impact | Effort | Priority |
|---|---------|--------|--------|----------|
| 1 | Salary Structure (formulas) | High | Medium | 🔴 Phase 4.1 |
| 2 | PayrollRun aggregate | High | Medium | 🔴 Phase 4.1 |
| 3 | Libya Tax Calculator (5%/10%) | High | Low | 🔴 Phase 4.1 |
| 4 | PayrollItem (per-employee) | High | Low | 🔴 Phase 4.1 |
| 5 | EOS Calculator | High | Medium | 🔴 Phase 4.2 |
| 6 | GL posting (JournalEntry) | High | Low | 🔴 Phase 4.2 |
| 7 | Payslip HTML view | Medium | Low | 🟡 Phase 4.2 |
| 8 | Frontend payroll pages | Medium | Medium | 🟡 Phase 4.3 |
| 9 | Payslip PDF generation | Medium | Medium | 🟢 Phase 4.4 |
| 10 | Bank/Email integration | Low | High | 🔵 Phase 4.5 |

---

## 8. Execution Strategy (Realistic for 15min workers)

الـ Phase 3 تعلّمنا: workers timeout عند 15min. الحل:
- **Tasks صغيرة** (≤15min لكل واحد)
- **Mavis takeover pattern** عند timeout
- **Parallel** للـ independent deliverables

### Task Breakdown:
| # | Task | Agent | Est. Time |
|---|------|-------|-----------|
| A | Research (هذا الملف) | Mavis | ✅ done |
| B | **Backend: SalaryStructure + PayrollRun + PayrollItem** | backend-architect | ~15min |
| C | **Backend: LibyaTaxCalculator + EosCalculator + Payroll services** | backend-architect | ~15min |
| D | **Backend: HrController extensions + GL posting event** | backend-architect | ~15min |
| E | **Frontend: payroll pages + payslip view** | frontend-engineer | ~15min |
| F | **E2E test (with Playwright MCP)** | qa-tester | ~10min |
| G | **AGENTS.md sync + CHANGELOG + HTML report** | Mavis | ✅ done by owner |

---

## 9. Files to Create/Update

### Backend (new):
- `src/backend/Modules/Payroll/Domain/Entities/{SalaryStructure,PayrollRun,PayrollItem,...}.cs`
- `src/backend/Modules/Payroll/Application/Services/{PayrollService,EosService,TaxService,...}.cs`
- `src/backend/Modules/Payroll/Infrastructure/{PayrollRepository,...}.cs`
- `src/backend/Shared/Migrations/20260624_100000_CreatePayrollTables.cs`
- `src/backend/Host/Controllers/HrController.cs` — extend with payroll endpoints
- `src/backend/Modules/Payroll/AGENTS.md`

### Frontend (new):
- `src/frontend/app/(authenticated)/hr/payroll/page.tsx`
- `src/frontend/app/(authenticated)/hr/payroll/[id]/page.tsx`
- `src/frontend/app/(authenticated)/hr/payroll/[id]/payslip/[empId]/page.tsx`
- `src/frontend/lib/api.ts` — extend `hrApi.payroll.*`

### Docs (new/updated):
- `docs/PLAN.md` — Phase 4 status updated
- `docs/CHANGELOG.md` — Phase 4 entry
- `docs/research/payroll-competitive.md` (optional)
- `docs/RELEASE-REPORT-PHASE4.html` (final deliverable)
- AGENTS.md (root + module-specific) — synced

---

## 10. Validation Plan

- Backend build: 0 errors
- Frontend build: clean
- E2E test (Playwright): create employee → create payroll run → process → verify payslip → post → verify GL entry
- CI: Backend + Frontend + Docker = all PASS

---

**Stop condition:** All entities + services + UI + E2E PASS + AGENTS.md synced + PR #11 merged to main.
