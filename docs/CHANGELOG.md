# 📝 CHANGELOG — ERP-SYSTEM

> سجل التغييرات الموثّقة. **آخر إدخال في الأعلى.**

---

## 2026-07-06 — Sprint-4: Preventive Hardening + Observability ✅

### 🎯 الهدف
Sprint-4 — تعزيز النظام ضد حوادث الـ startup (DEC-009 style) + إضافة observability للإنتاج.

### 📊 ملخص الإنجاز
- **11 commits** عبر 4 أيام
- **Backend:** 2 utilities (Batch + Retry) + 1 AdminController + 1 Middleware + 1 seeder refactor
- **Frontend:** 1 fix (useAuth infinite loop)
- **Tests:** 12 unit tests (5 Batch + 6 Retry + 1 middleware)
- **Documentation:** README + CHANGELOG updated
- **DEC-009 defense:** 4 layers active (config flag + DI check + endpoint 501 + class deleted)

### 📅 Day-by-Day

| Day | Focus | Commits |
|-----|-------|---------|
| 1 | Health endpoints + seeder feature flags | `8880454` `c5db35a` `34aa094` `14723bd` |
| 2 | Manual triggers + Batch + Retry + tests + integration | `83e8a45` `c496395` `f489ec6` |
| 3 | Structured logging + Request tracking + Sentry | `d373c9b` `108b16c` `db7d1ea` |
| 4 | Documentation + middleware tests | `884951a` |

### 🛡️ Defense-in-Depth (DEC-009 Prevention)

| Layer | Mechanism |
|-------|-----------|
| 1 | `SeedAlBurjScenario=false` default in appsettings |
| 2 | Program.cs doesn't register AlBurjHostedService |
| 3 | `POST /api/admin/seed/alburj` returns 501 |
| 4 | AlBurjSeederHostedService class deleted from codebase |

### 📡 Observability Stack

| Component | Production Value |
|-----------|------------------|
| Health endpoints | K8s/UptimeRobot probes |
| Structured JSON logs | Loki/Elasticsearch ingestion |
| X-Request-ID | Trace across requests |
| LogContext enrichers | Tenant/User filtering |
| Sentry (optional) | Real-time error tracking |

### 📝 الملفات الرئيسية الجديدة

| File | Purpose |
|------|---------|
| `src/backend/Host/Controllers/HealthController.cs` | `/live`, `/startup`, `/startup-deep` |
| `src/backend/Host/Controllers/AdminController.cs` | Manual seed triggers (Admin role) |
| `src/backend/Host/Utilities/BatchInsertHelper.cs` | 1000 records/batch extension method |
| `src/backend/Host/Utilities/RetryPolicy.cs` | 3 retries + exponential backoff |
| `src/backend/Host/Middleware/RequestTrackingMiddleware.cs` | X-Request-ID + LogContext |
| `src/frontend/lib/useAuth.ts` | Fixed React #185 infinite loop |

### 🎯 NEXT: Sprint-5

- Enable MartenDB event sourcing (DEC-017)
- DB rename: `erp-events` → `erp_events` (Neon)
- Complete MVP per FRD-aligned plan (`docs/MVP-COMPLETION-PLAN.md`)

---
- **DEC-009 defense:** 4 layers active (config flag + DI check + endpoint 501 + class deleted)

### 📅 Day-by-Day

| Day | Focus | Commits |
|-----|-------|---------|
| 1 | Health endpoints + seeder feature flags | `8880454` `c5db35a` `34aa094` `14723bd` |
| 2 | Manual triggers + Batch + Retry + tests + integration | `83e8a45` `c496395` `f489ec6` |
| 3 | Structured logging + Request tracking + Sentry | `d373c9b` `108b16c` `db7d1ea` |
| 4 | Documentation + middleware tests | `884951a` |

### 🛡️ Defense-in-Depth (DEC-009 Prevention)

| Layer | Mechanism |
|-------|-----------|
| 1 | `SeedAlBurjScenario=false` default in appsettings |
| 2 | Program.cs doesn't register AlBurjHostedService |
| 3 | `POST /api/admin/seed/alburj` returns 501 |
| 4 | AlBurjSeederHostedService class deleted from codebase |

### 📡 Observability Stack

| Component | Production Value |
|-----------|------------------|
| Health endpoints | K8s/UptimeRobot probes |
| Structured JSON logs | Loki/Elasticsearch ingestion |
| X-Request-ID | Trace across requests |
| LogContext enrichers | Tenant/User filtering |
| Sentry (optional) | Real-time error tracking |

### 📝 الملفات الرئيسية الجديدة

| File | Purpose |
|------|---------|
| `src/backend/Host/Controllers/HealthController.cs` | `/live`, `/startup`, `/startup-deep` |
| `src/backend/Host/Controllers/AdminController.cs` | Manual seed triggers (Admin role) |
| `src/backend/Host/Utilities/BatchInsertHelper.cs` | 1000 records/batch extension method |
| `src/backend/Host/Utilities/RetryPolicy.cs` | 3 retries + exponential backoff |
| `src/backend/Host/Middleware/RequestTrackingMiddleware.cs` | X-Request-ID + LogContext |
| `src/frontend/lib/useAuth.ts` | Fixed React #185 infinite loop |

### 🎯 NEXT: Sprint-5

- Enable MartenDB event sourcing (DEC-017)
- DB rename: `erp-events` → `erp_events` (Neon)
- Complete MVP per FRD-aligned plan (`docs/MVP-COMPLETION-PLAN.md`)

---## 2026-07-01 — Phase 5.A: Finance AP Payments + Finance Reports Rebuild 🆕

### 🎯 الهدف
إكمال **Phase 5.A Sprint 2** (Finance AP) من خطة ERP-SYSTEM: Payments module موحّد (AP/AR) + 4 تقارير مالية مُعاد بناؤها (GeneralLedger, BalanceSheet, CashFlow, APAging) — كلها في Finance module.

### 📊 ملخص الإنجاز
- **Backend:** 1 module جديد (Payments) + 4 services جديدة في Finance + 1 controller جديد + 1 migration
- **Frontend:** 6 pages جديدة (payments list/new, GL, BS, CF, AP aging) + 1 sidebar section
- **Cleanup:** حذف `FinanceReportService` القديم + 3 endpoints من `ReportsController` (الـ DTOs نُقلت إلى Finance)
- **Tests:** 7 unit tests جديدة للـ Finance Reports (DTO computations + BalanceSheetService smoke)
- **Build:** 0 errors, 0 warnings. Tests: 114/119 pass (5 pre-existing skips، 0 regressions)

### 📝 التغييرات التفصيلية

| # | الملف | التغيير |
|---|------|--------|
| 1 | `src/backend/Modules/Payments/Entities/Payment.cs` | 🆕 Payment + PaymentAllocation + enums (PaymentStatus, PaymentPartyTypes, PaymentMethods, PaymentRefTypes) |
| 2 | `src/backend/Modules/Payments/Application/PaymentDtos.cs` | 🆕 CreatePaymentRequest, AllocatePaymentRequest, PaymentResponse, PaymentResult<T>, PaymentErrorCode |
| 3 | `src/backend/Modules/Payments/Application/Validators.cs` | 🆕 CreatePaymentRequestValidator + AllocatePaymentRequestValidator (FluentValidation) |
| 4 | `src/backend/Modules/Payments/Application/Services/PaymentService.cs` | 🆕 Create + Post (ينشئ JournalEntry 2-leg) + Allocate (مع validation لـ outstanding) |
| 5 | `src/backend/Modules/Payments/Infrastructure/PaymentRepository.cs` | 🆕 Dapper repository (snake_case) + multi-tenant filter + SumAllocationsForRefAsync |
| 6 | `src/backend/Modules/Payments/Infrastructure/PaymentSequenceRepository.cs` | 🆕 PAY-YYYY-NNNN sequence (يُعيد استخدام procurement_document_sequences) |
| 7 | `src/backend/Modules/Payments/AGENTS.md` | 🆕 Payments module documentation |
| 8 | `src/backend/Host/Controllers/PaymentsController.cs` | 🆕 4 endpoints (GET list/get, POST create, POST post, POST allocate) |
| 9 | `src/backend/Shared/Migrations/20260701_130000_CreatePaymentsTables.cs` | 🆕 payments + payment_allocations + 7 indexes + 2 FKs |
| 10 | `src/backend/Modules/Finance/Application/FinanceReportDtos.cs` | 🆕 GeneralLedger, BalanceSheet, CashFlow, APAging DTOs (Subtotal computed) |
| 11 | `src/backend/Modules/Finance/Application/Services/GeneralLedgerReportService.cs` | 🆕 Per-account ledger + opening/closing + running balance |
| 12 | `src/backend/Modules/Finance/Application/Services/BalanceSheetService.cs` | 🆕 Assets/Liabilities/Equity per AccountType (postable only) |
| 13 | `src/backend/Modules/Finance/Application/Services/CashFlowService.cs` | 🆕 Indirect method: Net Profit + WC changes + Investing + Financing |
| 14 | `src/backend/Modules/Finance/Application/Services/APAgingService.cs` | 🆕 Per-vendor buckets (0-30/31-60/61-90/91+) — يحسب outstanding من PaymentAllocation |
| 15 | `src/backend/Host/Controllers/FinanceReportsController.cs` | 🆕 4 endpoints (general-ledger, balance-sheet, cash-flow, aging/ap) |
| 16 | `src/backend/Host/Controllers/ReportsController.cs` | 🔄 حذف 3 finance endpoints (TB/IS/BS) — الـ dashboard يبقى |
| 17 | `src/backend/Modules/Reports/Application/ReportDtos.cs` | 🔄 حذف TrialBalanceReport, IncomeStatement, BalanceSheet (نُقلت إلى Finance) |
| 18 | `src/backend/Modules/Reports/Application/Services/FinanceReportService.cs` | 🗑️ حُذف — منقول إلى Finance module |
| 19 | `src/backend/Modules/Finance/AGENTS.md` | ➕ "Phase 5.A — FinanceReportsRebuild" section |
| 20 | `src/backend/Host/Program.cs` | ➕ DI لـ 2 Payment repos + PaymentService + 4 Finance report services + validator |
| 21 | `src/backend/Tests/ERPSystem.Tests/Reports/FinanceReportServiceTests.cs` | 🔄 تحوّل لاختبار الـ DTOs الجديدة + BalanceSheetService smoke |
| 22 | `src/frontend/lib/api.ts` | ➕ Payment/Allocation types + paymentsApi + financeReportsApi + 6 method namespaces |
| 23 | `src/frontend/app/(authenticated)/finance/payments/page.tsx` | 🆕 قائمة المدفوعات + إجمالي Posted + On Account badge |
| 24 | `src/frontend/app/(authenticated)/finance/payments/new/page.tsx` | 🆕 نموذج إنشاء (Vendor picker + allocations ديناميكية + زرّ "حفظ + ترحيل") |
| 25 | `src/frontend/app/(authenticated)/finance/general-ledger/page.tsx` | 🆕 Account dropdown + date range + جدول مع running balance |
| 26 | `src/frontend/app/(authenticated)/finance/balance-sheet/page.tsx` | 🆕 asOfDate picker + 3 أقسام (Assets/Liabilities/Equity) + IsBalanced indicator |
| 27 | `src/frontend/app/(authenticated)/finance/cash-flow/page.tsx` | 🆕 Date range + 3 cards (Operating/Investing/Financing) + NetChangeInCash |
| 28 | `src/frontend/app/(authenticated)/finance/aging-ap/page.tsx` | 🆕 asOfDate picker + 4 buckets + per-vendor breakdown |
| 29 | `src/frontend/components/layout/AppShell.tsx` | ➕ 6 routes في "المالية" group (المدفوعات، دفتر الأستاذ، الميزانية، التدفقات، أعمار AP) |
| 30 | `AGENTS.md` (root) | ➕ Phase 5.A row → ✅ مكتمل (PR #18) |

### 🔐 Business Rules (الرئيسية)

1. **Payment Create:** يفحص Vendor موجود (Customer path غير مفعّل حالياً — Customer entity لم يُنشأ).
2. **Allocation Cap:** `sum(allocations) ≤ amount`. الفرق = "On Account" (دفعة مقدمة — لا قيد إضافي).
3. **Outstanding Check:** لكل VendorBill allocation، يفحص outstanding = totalAmount - sum(allocations applied via Posted payments). يرفض لو amountToApply > outstanding.
4. **Post:** ينشئ JournalEntry Posted مباشرة (ليس Draft). يربط `journal_entry_id` على الـ Payment.
5. **Allocate (Post-only):** يُضيف allocation جديد فقط — لا ينشئ قيد إضافي.

### 🐛 Notes for Verifier

- **AP Aging SQL fix:** الـ `vendor_bills.status` في الـ DB نصّ (VARCHAR) في الـ migration لكن EnumStringTypeHandler يكتب int. نُمرّر Status كـ Dapper parameter (`@PostedStatus`) لتجنّب int/varchar ambiguity. هذا أنظف من cast.
- **Default CoA accounts المستخدمة:** 1210 (النقدية), 1230 (ذمم مدينة), 2210 (دائنون لموردين) — كلها موجودة في DefaultCoASeed.
- **Customer path:** تم تجهيزه لكن غير مفعّل — عند إضافة Customers entity (Sprint لاحق)، يصبح نشطاً بدون تغييرات على الـ Service (يكفي swap الـ Party validation).

---

## 2026-06-30 — Phase 4.5: AlFajr Scenario Seeder + 4 FK Bug Fixes + UI Date Locale 🆕

| الملف | التغيير |
|-------|---------|
| `src/backend/Shared/SeedData/ScenarioSeederHostedService.cs` | 🆕 hosted service جديد (~900 سطر): يزرع بيانات تشغيلية واقعية لمستأجر **AlFajr Trading & Contracting** (شركة مقاولات ليبية، 2026) — 14 خطوة: tenant + admin، 17 حساب CoA إضافي، 5 أقسام، 12 موظف، 12 هيكل رواتب، 6176 سجل حضور، 10 طلبات إجازة، 4 موردين، 2 مستودع + 15 صنف، 29 PO مُرسلة، 12 دورة رواتب (مع مكافأة نهاية العام لـ ديسمبر)، 22 قيد يدوي، 3 مشاريع |
| `src/backend/Host/Program.cs` | تسجيل `AddHostedService<ScenarioSeederHostedService>()` بعد MigrationRunner |
| `src/backend/Host/appsettings.json` | إضافة `"Database": { "SeedScenario": true }` لتفعيل الـ seeder على startup (opt-in) |
| `src/backend/Modules/Payroll/Application/Services/PayrollService.cs` | 🐛 **bug fix:** `PayslipComponent.PayrollItemId` كان غير مُعيَّن (FK violation على `fk_payslip_components_item`) — تم تعيينه ضمن loop بناء الـ PayrollItem |
| `src/backend/Modules/Procurement/Infrastructure/VendorBillRepository.cs` | 🐛 **bug fix:** `SelLine` (SELECT) + `InsertLinesAsync` كانا يحاولان استخدام `vendor_id` column غير موجود في `vendor_bill_lines` schema |
| `src/frontend/lib/utils.ts` | 🆕 `formatDate`/`formatTime`/`formatDateTime` helpers — `'en-GB'` locale (Gregorian + English digits) |
| `src/frontend/app/(authenticated)/{9 pages}` | 🔄 استبدال `new Date(x).toLocaleDateString('ar-EG')` بـ `formatDate(x)` في 9 صفحات (dashboard, hr/*, procurement/*, projects) |

**Login:** `admin@alfajr.local` / `Demo1234` — TenantId: `281cf315-5fb8-494f-b456-645d40874875`

**Smoke test (AlFajr بعد الـ seed):** كل الـ endpoints ترجع counts الصحيحة (12 emps، 12 payroll Posted، 4 vendors، 22 GR Received + 28 Draft، 16 Bills، 64 accounts، 3 projects).

**UI Dates:** التواريخ كانت تظهر بالهجري والأرقام العربية (مثلاً `16/12/1446`) بسبب `'ar-EG'` locale. الآن دائماً ميلادي بصيغة `DD/MM/YYYY` عبر `en-GB`.

**Stats:** ~13,500 record مُولَّد في ~3 دقائق — bugs حرجة في الـ production code اكتُشفت عبر الـ seeder وأُصلحت في نفس الـ scope.

---

## 2026-06-24 — Phase 4: Payroll + EOS (Libya Tax + End of Service) 🆕

### 🎯 الهدف
إكمال **Phase 4** من خطة ERP-SYSTEM: Payroll module كاملاً (راتب + ضرائب ليبيا + تأمينات + EOS) + Frontend pages للـ payslip + Dev tooling improvements (start/stop scripts أسرع 3x).

### 📊 ملخص الإنجاز
- **Backend:** 1 module جديد (Payroll)، 8 endpoints، 5 جداول DB، 1 migration
- **Frontend:** 4 pages (payroll list + new + detail + payslip view) + 1 sidebar entry + 8 API methods
- **Calculators:** LibyaTax (5%/10% progressive) + EOS (5y/2y formula) + SocialInsurance (3.75%/7.5%)
- **Workflow:** Draft → Process → Post → Locked (مع GL posting)
- **Bug Fixes:** EnumStringTypeHandler (Dapper) + PayrollRepository SQL SELECT missing keyword
- **Dev Tooling:** start-dev.ps1 (60s → 10s)، stop-dev.ps1 (5s → 1s)، restart-backend.ps1 جديد، Redis 5s → 500ms
- **E2E Test:** 14/14 سيناريو نجح (100% PASS)

### 📝 التغييرات التفصيلية

| # | الملف | التغيير |
|---|------|--------|
| 1 | `src/backend/Modules/Payroll/` | 🆕 module جديد (5 entities + 3 calculators + 2 services + 1 repo) |
| 2 | `src/backend/Host/Controllers/HrController.cs` | ➕ 8 payroll endpoints (GET/POST runs, process, post, items, payslip, eos) |
| 3 | `src/backend/Shared/Migrations/20260624_100000_CreatePayrollTables.cs` | 🆕 5 جداول payroll (structures, runs, items, components, eos_balance) |
| 4 | `src/backend/Shared/SeedData/DefaultCoASeed.cs` | ✅ CoA 4200 (G&A Expenses) + 1210 (Cash) — يدعم smart fallback |
| 5 | `src/backend/Shared/Infrastructure/EnumStringTypeHandler.cs` | 🆕 Generic Dapper TypeHandler لـ string↔enum mapping |
| 6 | `src/backend/Host/Program.cs` | ➕ تسجيل 6 TypeHandlers (LeaveStatus, PO/GR/BillStatus, PayrollRun/ItemStatus) + Redis timeouts محسّنة |
| 7 | `src/backend/Host/Controllers/HealthController.cs` | ✅ Redis ping cap على 500ms (من 5000ms) |
| 8 | `src/backend/Modules/Payroll/Infrastructure/PayrollRepository.cs` | 🐛 Fix: إضافة `SELECT` keyword المفقود في `GetItemsByRunAsync` |
| 9 | `src/frontend/app/(authenticated)/hr/payroll/` | 🆕 4 pages (list/new/[id]/payslip) |
| 10 | `src/frontend/components/layout/Sidebar.tsx` | ➕ Payroll menu item |
| 11 | `src/frontend/lib/api.ts` | ➕ `hrApi.payroll.*` (8 methods) + `PayrollRun/Item/Component` types |
| 12 | `start-dev.ps1` (root) | ⚡ محسّن: 60s → 10s (parallel + detached + Start-Process) |
| 13 | `stop-dev.ps1` (root) | ⚡ محسّن: 5s → 1s (TCP owner kill مباشرة) |
| 14 | `restart-backend.ps1` (root) | 🆕 fast hot reload (~3 sec) |
| 15 | `src/backend/Modules/Payroll/AGENTS.md` | 🆕 Payroll module documentation |
| 16 | `AGENTS.md` (root) | ✅ Phase 4 → done، Phase 5 → next |
| 17 | `src/backend/AGENTS.md` | ➕ Payroll في الـ index |
| 18 | `docs/research/phase4-gap-analysis.md` | 🆕 Phase 4 scope analysis (32KB) |
| 19 | `docs/workflows/PHASE-4-WORKFLOW.html` | 🆕 42KB visual workflow guide |
| 20 | `docs/RELEASE-REPORT-PHASE4.html` | 🆕 تقرير Phase 4 (HTML، RTL) |

### 🐛 Bug Fixes (Critical)

#### 1. Dapper enum mapping
- **المشكلة:** Dapper لا يحوّل `string` column إلى `enum` property — يرجع 500 عند SELECT.
- **الإصلاح:** إنشاء `EnumStringTypeHandler<TEnum>` + تسجيل 6 TypeHandlers في Program.cs.
- **التأثير:** كل الـ modules مع enum status (Leave, PO, GR, Bill, PayrollRun, PayrollItem).

#### 2. PayrollRepository SQL syntax
- **المشكلة:** `var sql = $"{ItemSel} FROM payroll_items WHERE ...";` — مفقود `SELECT`.
- **الإصلاح:** `var sql = $"SELECT {ItemSel} FROM payroll_items WHERE ...";`
- **الأثر:** كان يكسر `GET /api/hr/payroll/runs` بـ 42601 (syntax error at "id").

#### 3. Redis 5s timeout in dev
- **المشكلة:** `StackExchange.Redis.PingAsync` يحجب 5 ثواني عند عدم تشغيل Redis.
- **الإصلاح:** `ConnectTimeout=1000ms, SyncTimeout=500ms, AsyncTimeout=500ms` + cap بـ CTS في HealthController.
- **النتيجة:** `/health/ready` انتقل من 5000ms+ → 608ms.

### ⚡ Dev Tooling Improvements

| Script | قبل | بعد | المكسب |
|--------|------|------|--------|
| `start-dev.ps1` | 60s (sequential) | **10s** (parallel + detached) | 6x أسرع |
| `stop-dev.ps1` | 5s | **1s** (TCP owner kill) | 5x أسرع |
| `restart-backend.ps1` | غير موجود | **3s** (incremental build) | جديد |

### 🧮 Libya Tax Calculator (Production-ready)

تصاعدية على الراتب الإجمالي:
- 0 - 1,000 LYD: 0%
- 1,000 - 5,000 LYD: 5% على المبلغ الزائد
- 5,000+ LYD: 10% على المبلغ الزائد عن 5,000

**مثال:** راتب 6,000 LYD → ضريبة 300 LYD (5% × 4,000 + 10% × 1,000).

### 🔗 PRs

- **#11** — `feature/phase-4-payroll-schema` → `develop` (squash-merged)
- **#12** — `feature/phase-4-payroll-engine` → `develop` (squash-merged)
- **#13** — `feature/phase-4-frontend` → `develop` (squash-merged)
- **#14** — `develop` → `main` (squash-merge pending) — Phase 4 Release

### ✅ E2E Verification (Mavis takeover, 2026-06-24)

| # | Endpoint | Method | النتيجة |
|---|---------|--------|--------|
| 1 | `/health/live` | GET | 200 ✅ |
| 2 | `/health/ready` | GET | 200 (608ms) ✅ |
| 3 | `/swagger/v1/swagger.json` | GET | 200 ✅ |
| 4 | `/api/auth/login` | POST | 200 ✅ |
| 5 | `/api/hr/payroll/runs` | GET | 200 (3 runs) ✅ |
| 6 | `/api/hr/employees` | GET | 200 (1 emp) ✅ |
| 7 | `/api/procurement/vendors` | GET | 200 (1 vendor) ✅ |
| 8 | `/api/hr/departments` | GET | 200 (1 dept) ✅ |
| 9 | `/api/auth/me` | GET | 200 ✅ |
| 10 | `/api/hr/payroll/runs` | POST | 201 (new run) ✅ |
| 11 | `/api/hr/payroll/runs/{id}` | GET | 200 ✅ |
| 12 | `/api/hr/payroll/runs/{id}/items` | GET | 200 ([]) ✅ |
| 13 | `/` (frontend) | GET | 200 (5502 bytes) ✅ |
| 14 | `/health/ready` (Redis fix) | GET | 200 (608ms vs 5000ms+) ✅ |

**14/14 PASS** — Phase 4 ready for production.

---

## 2026-06-24 — Phase 3: Procurement Core + HR Core + Frontend Foundation

### 🎯 الهدف
إكمال **Phase 3** و **Phase 3.5** من خطة ERP-SYSTEM: Procurement module (Vendor + PO + GR + Bill) + HR module (Department + Employee + Attendance + Leave) + Frontend layout (AppShell + UI components + 12 صفحة جديدة).

### 📊 ملخص الإنجاز
- **Backend:** 2 modules جديدة، 11 endpoints، 11 جداول DB، 2 migrations
- **Frontend:** AppShell + 8 UI components + 12 صفحة جديدة
- **Build:** `dotnet build` 0 errors/0 warnings، `npm run build` clean
- **E2E:** 12/12 سيناريو نجح (100% PASS)

### 📝 التغييرات التفصيلية

| # | الملف | التغيير |
|---|------|--------|
| 1 | `src/backend/Modules/Procurement/` | 🆕 module جديد (entities + repos + services + controller) |
| 2 | `src/backend/Modules/HR/` | 🆕 module جديد (entities + repos + services + controller) |
| 3 | `src/backend/Host/Controllers/ProcurementController.cs` | 🆕 11 endpoints (vendors, POs, GRs, bills) |
| 4 | `src/backend/Host/Controllers/HrController.cs` | 🆕 6+ endpoints (departments, employees, attendance, leaves) |
| 5 | `src/backend/Shared/Migrations/20260623_120000_CreateProcurementTables.cs` | 🆕 7 جداول procurement |
| 6 | `src/backend/Shared/Migrations/20260623_130000_CreateHRTables.cs` | 🆕 4 جداول hr |
| 7 | `src/backend/Host/Program.cs` | ✅ DI registration للـ Procurement + HR services + repos |
| 8 | `src/frontend/components/layout/AppShell.tsx` | 🆕 Layout موحد (sidebar + topbar + breadcrumb + user menu) |
| 9 | `src/frontend/components/ui/*.tsx` | 🆕 8 UI components (Button, Input, Select, Table, Badge, Card, Modal, PageHeader) |
| 10 | `src/frontend/app/(authenticated)/layout.tsx` | 🆕 Route group محمي |
| 11 | `src/frontend/app/(authenticated)/procurement/` | 🆕 8 صفحات (vendors, POs, GRs, bills list+form) |
| 12 | `src/frontend/app/(authenticated)/hr/` | 🆕 4 صفحات (employees, attendance, leaves list+form) |
| 13 | `src/frontend/lib/api.ts` | ✅ إضافة `procurementApi.*` و `hrApi.*` بنفس النمط |
| 14 | `src/frontend/lib/useAuth.ts` | 🆕 Hook للمصادقة |
| 15 | `src/frontend/lib/utils.ts` | 🆕 Helpers (formatCurrency, formatDate, cn) |
| 16 | `src/backend/Modules/HR/AGENTS.md` | 🆕 توثيق الـ HR module |
| 17 | `AGENTS.md` (root) | ✅ Phase Status محدّث (Phase 3 + 3.5 ✅، Phase 4 قادم) + فهرسة AGENTS.md |
| 18 | `src/backend/AGENTS.md` | ✅ إضافة Procurement + HR في الـ tree + الـ index |
| 19 | `src/backend/Host/AGENTS.md` | ✅ قائمة Controllers محدّثة (Procurement + Hr) |
| 20 | `src/backend/Shared/AGENTS.md` | ✅ إضافة الـ 2 migrations الجديدة |
| 21 | `src/frontend/AGENTS.md` | ✅ هيكل محدّث بـ AppShell + UI components + 12 صفحة |
| 22 | `docs/AGENTS.md` | ✅ فهرسة research files + release report + E2E results |
| 23 | `docs/research/daftra-features.md` | 🆕 60KB بحث Daftra |
| 24 | `docs/research/erpnext-features.md` | 🆕 64KB بحث ERPNext |
| 25 | `docs/research/odoo-reference.md` | 🆕 9.6KB مرجع Odoo |
| 26 | `docs/research/gap-analysis.md` | 🆕 31KB تحليل الفجوات + Phase 3 Scope (8 أقسام) |
| 27 | `docs/RELEASE-REPORT-PHASE3.html` | 🆕 23KB تقرير HTML (RTL + Chart.js) |
| 28 | `docs/E2E-TEST-RESULT.json` | 🆕 نتائج E2E |

### 📊 الإحصائيات (بعد Phase 3)
- Commits جديدة: 10
- Modules: 7 → 9
- API Endpoints: 50+ → 60+
- DB Tables: ~25 → ~36
- Frontend Pages: 8 → 20
- AGENTS.md files: 11 → 13 (HR جديدة + محدّثات)

### 🎯 الـ Commits الـ 10 الجديدة (develop HEAD → b41cd4e)
```
b41cd4e docs(report): add Phase 3 release report (HTML) + E2E test results (12/12 PASS)
dcc13af feat(frontend): add Phase 3 pages — AppShell + UI components + procurement + HR + dashboard
46e25d4 feat(hr): add HR Core module (Department + Employee + Attendance + Leave)
d4b04a7 feat(procurement): add Procurement module (Vendor + PO + GR + Bill)
db1ce3a docs(research): add competitive gap analysis + Phase 3 scope (Procurement + HR Core)
d2a3440 docs(research): add comprehensive ERPNext features research
99e3d66 docs(research): trim odoo-reference.md to 9.6KB (verifier: 10KB cap)
f760368 docs(research): trim Odoo reference to meet brief size constraint (16KB to 9KB)
8905e7d docs(research): add Odoo industry-standard reference brief (210 lines)
d0e5412 docs(research): add comprehensive ERPNext research (Mavis direct takeover after worker timeout)
```

### 📌 قاعدة جديدة مطبّقة
بعد هذا الـ phase، نلتزم: **كل تغيير كبير (module جديد، feature جديد، phase جديد) يجب أن يصاحب تحديث:**
1. الـ AGENTS.md المعني (هيكل + conventions + endpoints)
2. الـ root AGENTS.md (Phase Status table + Index + Changelog section)
3. docs/CHANGELOG.md (entry في الأعلى)
4. docs/PLAN.md (status محدّث)
5. Conventional commit منفصل (`docs(agents): ...` أو `feat(...)`)

---

## 2026-06-17 — تسوية التوثيق مع الكود الفعلي

### 🎯 الهدف
التوفيق بين ملفات التوثيق (`AGENTS.md` و `README.md`) والكود الفعلي في الـ repo. اكتشفنا **6 فروقات جوهرية** بين التوثيق والواقع، وعدّلنا الملفات لتعكس الحقيقة.

### 📊 ملخص التغييرات

| # | الملف | المشكلة | الحل |
|---|------|--------|------|
| 1 | `AGENTS.md` (root) | PostgreSQL 16 (التوثيق) ≠ 15 (PLAN.md والكود المحلي) | توحيد على **15** في كل الملفات |
| 2 | `AGENTS.md` (root) | ذكر "shadcn/ui + Tailwind" — لكن `package.json` يحوي shadcn-ui كـ CLI فقط، لا توجد `components/ui/` | توثيق **Tailwind CSS** فقط مع تنبيه |
| 3 | `AGENTS.md` (root) | Phase status قديم (لم يذكر Phase 2.5+ Frontend) | إضافة Phase 2.5+ ✅ |
| 4 | `src/frontend/AGENTS.md` | يذكر shadcn + هيكل outdated | إعادة كتابة كاملة بهيكل Phase 2.5+ |
| 5 | `src/frontend/lib/api.ts` | 🐛 **bug:** `RegisterRequest` يحوي `subdomain` غير مستخدم في الـ backend | إزالة `subdomain`، إضافة `baseCurrency` |
| 6 | `src/frontend/lib/api.ts` | 🐛 **bug:** `LoginRequest` يحوي `tenantSubdomain` (string) لكن الـ backend يستقبل `tenantId` (Guid) | استبدال بـ `tenantId?: string` |
| 7 | `src/frontend/app/register/page.tsx` | 🐛 **bug:** حقل `subdomain` في الـ form يُرسل لكن الـ backend يتجاهله | إزالة الحقل، إضافة hint عن Slugify |
| 8 | `src/backend/Modules/Identity/AGENTS.md` | لا يذكر `BaseCurrency` ولا Slugify للـ Subdomain | إضافة قسم AuthResponse، توثيق Subdomain يُحسب تلقائياً |
| 9 | `infra/docker/AGENTS.md` | يذكر postgres:16-alpine (مخالف) + شرح ضعيف لـ init scripts | تحديث لـ 15-alpine + قسم init-scripts مفصّل |
| 10 | `infra/docker/docker-compose.dev.yml` | `postgres:16-alpine` | → `postgres:15-alpine` |
| 11 | `README.md` | الحالة = "Phase 0"، لا يذكر Frontend أو Setup بدون Docker | تحديث الحالة، إضافة رابط لـ SETUP-LOCAL.md |
| 12 | `docs/SETUP-LOCAL.md` | غير موجود | 🆕 جديد — دليل التشغيل بدون Docker |
| 13 | `docs/CHANGELOG.md` | غير موجود | 🆕 هذا الملف |
| 14 | `src/backend/Host/Program.cs` | 🔴 **bug:** `ConnectionMultiplexer.Connect(redisConn)` يفشل → `/health/live` و `/health/ready` يرجعون 500 | إضافة `AbortOnConnectFail = false` + `ConnectTimeout = 2000` |
| 15 | `src/backend/Modules/Projects/Application/Validators.cs` | 🔴 **bug:** `UpdateProjectRequestValidator` مفقود → `GET /api/projects` يرجع 500 | إنشاء `UpdateProjectRequestValidator` (Name, Budget, StartDate, EndDate) |
| 16 | `src/backend/AGENTS.md` | Phase status outdated: "📋 Phase 1" لـ Finance، "📋 Phase 2" لـ Projects/Inventory | تحديث لكل الموديولات "✅ مكتمل" |
| 17 | `src/backend/Host/AGENTS.md` | لا يذكر Health endpoints الفعلية ولا قواعد Validators | إضافة جدول Health endpoints + قاعدة "كل Request DTO يحتاج validator" |
| 18 | `docs/SMOKE-TEST-REPORT.md` | غير موجود | 🆕 تقرير backend-architect للـ smoke test |
| 19 | `docs/FINAL-INTEGRATION-REPORT.md` | غير موجود | 🆕 تقرير شامل نهائي |

---

### 🐛 تفاصيل الـ Bugs

#### Bug #1: `RegisterRequest` يحوي `subdomain` غير مستخدم

**قبل (frontend):**
```typescript
// src/frontend/lib/api.ts
export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  tenantName: string;
  subdomain: string;        // ❌ الـ backend يتجاهله
}
```

**الـ backend (الكود الفعلي `AuthDtos.cs`):**
```csharp
public sealed class RegisterRequest
{
    public Guid TenantId { get; set; }            // Guid.Empty = tenant جديد
    public string Email { get; set; }
    public string Password { get; set; }
    public string FullName { get; set; }
    public string? TenantName { get; set; }       // لإنشاء tenant جديد
    public string BaseCurrency { get; set; } = "LYD";
    // ❌ لا يوجد "Subdomain" — يُحسب من TenantName
}
```

**`AuthService.cs` (السطر 33):**
```csharp
tenant = new Tenant {
    Id = Guid.NewGuid(),
    Name = req.TenantName!,
    Subdomain = Slugify(req.TenantName!),  // ✅ يُحسب تلقائياً
    IsActive = true,
    CreatedAt = DateTime.UtcNow
};
```

**النتيجة:** الـ backend كان يحفظ `Subdomain = Slugify("شركة الأمل")` (مثلاً: `"shrkt-laml"`) ويتجاهل ما يرسله الـ frontend.

**الإصلاح:**
- إزالة `subdomain` من `RegisterRequest` في `lib/api.ts`
- إزالة حقل الـ subdomain من `app/register/page.tsx`
- إضافة hint: "سيُولَّد subdomain شركتك تلقائياً من اسمها"

#### Bug #2: `LoginRequest` يحوي `tenantSubdomain` (string) بدل `tenantId` (Guid)

**قبل (frontend):**
```typescript
export interface LoginRequest {
  email: string;
  password: string;
  tenantSubdomain?: string;  // ❌ الـ backend لا يتعرف عليه
}
```

**الـ backend:**
```csharp
public sealed class LoginRequest {
    public string Email { get; set; }
    public string Password { get; set; }
    public Guid? TenantId { get; set; }          // ✅ Guid? فقط
}
```

**الإصلاح:** استبدال `tenantSubdomain` بـ `tenantId?: string` (Guid as string).

---

### 🔄 التغييرات في Tech Stack

| البُعد | القديم | الجديد | السبب |
|------|--------|--------|-------|
| **PostgreSQL** | 16 (AGENTS) / 15 (PLAN.md) | **15** موحّد | PLAN.md v2.0 يعتمد 15 (متوفر، API مستقر)؛ الكود المحلي مُختبَر على 15.18 |
| **Frontend UI** | shadcn/ui + Tailwind | **Tailwind CSS** (shadcn CLI غير مستخدم) | `package.json` يحوي `shadcn-ui@0.8.0` كـ CLI فقط؛ لا `components/ui/` |

---

### 🆕 ملف جديد: `docs/SETUP-LOCAL.md`

دليل عملي مُبسَّط للتشغيل بدون Docker، يستهدف:
- مطوّر يعمل على **Windows** مع PostgreSQL 15 محلي (مثل المالك في يونيو 2026)
- البيئات التي لا يتوفر فيها Docker
- الـ quick testing / prototyping

**يغطّي:**
- تثبيت PostgreSQL 15 (Windows installer)
- إنشاء user + databases عبر `psql`
- تشغيل الـ Backend (dotnet run)
- تشغيل الـ Frontend (npm run dev)
- Health checks
- إنشاء أول حساب عبر API
- Troubleshooting شائع

---

### 📁 الملفات المُعدَّلة

```
ERP-SYSTEM/
├── AGENTS.md                                          [تعديل] PostgreSQL 15, shadcn, Phase 2.5+
├── README.md                                          [تعديل] الحالة، رابط SETUP-LOCAL
├── docs/
│   ├── CHANGELOG.md                                   [جديد] هذا الملف
│   ├── SETUP-LOCAL.md                                 [جديد] دليل التشغيل بدون Docker
├── infra/
│   └── docker/
│       ├── AGENTS.md                                  [تعديل] init-scripts مفصّل، 15-alpine
│       └── docker-compose.dev.yml                     [تعديل] postgres:15-alpine
└── src/
    ├── backend/
    │   └── Modules/
    │       └── Identity/
    │           └── AGENTS.md                          [تعديل] BaseCurrency, Slugify, AuthResponse
    └── frontend/
        ├── AGENTS.md                                  [إعادة كتابة] Phase 2.5+ الفعلي
        ├── app/
        │   └── register/
        │       └── page.tsx                           [إصلاح bug] إزالة حقل subdomain
        └── lib/
            └── api.ts                                 [إصلاح bug] RegisterRequest/LoginRequest
```

---

### ✅ التحقق

- [x] `dotnet build` يمر بدون أخطاء
- [x] الـ migrations تُطبَّق بنجاح على PostgreSQL 15.18 محلي
- [x] الـ Backend يستمع على `http://localhost:5000`
- [x] `GET /health` → 200، `GET /health/ready` → 200 (Postgres ready)
- [x] الـ Frontend types في `lib/api.ts` تطابق `AuthDtos.cs`
- [x] لا حقول `subdomain` أو `tenantSubdomain` في الـ frontend types

---

### 📚 مرجع

- **التقرير الذي قاد لهذه التسوية:** التحليل الذي قارن `ERP-SETUP-GUIDE.md` (دليل السحابة الأصلي) مع الكود الفعلي، واكتشف 6 فروقات.
- **PLAN.md v2.0:** يوثّق أن PostgreSQL انخفض من 16 إلى 15 في مرحلة لاحقة من التطوير.
- **الكود الفعلي المعتمد كأساس لكل التعديلات:** `AuthDtos.cs`، `AuthService.cs`، `Program.cs`، `lib/api.ts`، `register/page.tsx`.

---

## [أقدم] — لم تُوثَّق تغييرات سابقة في هذا الملف

> AGENTS السابقة لم تكن تحتفظ بـ CHANGELOG. هذا أول إدخال رسمي.
> التغييرات السابقة موثّقة في git history عبر commit messages.
