# Phase 5 Plan: Finance & Projects Enhancement

**Reference:** `FRD-ERP-Holding-Libya.md` (2026-06-24)
**Focus:** Finance + Projects modules only (skip Tax, Manufacturing, POS)
**Constraint:** لا نفرط في التعقيد — features معقدة تتأجل لـ releases لاحقة

---

## 1. تحليل FRD ↔ النظام الحالي

### Finance sub-modules (FRD §4)

| Sub-module | FRD ID | Status | ما ينقص |
|---|---|---|---|
| **FM-1 General Ledger** | GL-FR-01..12 | ✅ Phase 1 | Period Locking (10), Fiscal Year UI (11) |
| **FM-2 AR (ذمم مدينة)** | AR-FR-01..10 | ❌ غير موجود | Customers + SalesInvoices + Receipts + Aging + Credit Note + Customer Statement + Progress Invoice |
| **FM-3 AP (ذمم دائنة)** | AP-FR-01..08 | ⚠️ جزئي | Vendors (✅ Procurement) + VendorBills (✅ basic) + **Payment entity + Aging AP + Debit Note مفقود** |
| **FM-4 Bank Reconciliation** | BR-FR-01..05 | ❌ | BankStatement + manual match |
| **FM-5 Cash & Bank** | CB-FR-01..05 | ❌ | BankAccount entity + petty cash |
| **FM-6 Fixed Assets** | FA-FR-01..08 | ❌ | Asset + Depreciation (Straight-Line) + Disposal |
| **FM-7 Budgeting** | BG-FR-01..05 | ⚠️ Project-level فقط | Standalone Budget vs Actual |
| **FM-8 Multi-Currency** | MC-FR-01..06 | ❌ | Currency + ExchangeRate + per-JE FX |
| **FM-9 Cost Centers** | CC-FR-01..05 | ⚠️ Basic | Cost Center Report + Profit Center |
| **FM-10 Project Accounting** | PJ-FR-01..10 | ❌ Most features | Customer link + **Retainage + Progress Invoice + Subcontractor** + Cost Center auto-create + IFRS 15 |
| **FM-11 Intercompany** | IC-FR-01..05 | ❌ | IC journals + consolidation (deferred) |
| **FM-12 Financial Reporting** | FR-FR-01..10 | ⚠️ Partial | Trial Balance ✅ + Income Statement ✅ + **Balance Sheet + Cash Flow + GL Report per account + Aging مفقود** |
| **FM-13 Tax** | TX-FR-01..07 | ❌ | **مؤجل بطلب المستخدم** |
| **FM-14 Audit Trail** | AT-FR-01..04 | ❌ | Audit log entity (future) |

### Projects Module (FRD §7.5)

| FR ID | Status | ما ينقص |
|---|---|---|
| PRJ-FR-01 Project CRUD | ✅ Phase 2.1 | Add `CustomerId` field |
| PRJ-FR-02 Status workflow | ✅ | OK |
| PRJ-FR-03 Tasks + Resources | ✅ | OK |
| PRJ-FR-04 Budget vs Actual | ✅ | UI polish |
| PRJ-FR-05 **Retainage %** | ❌ | حقل + حساب |
| PRJ-FR-06 **Progress Invoice (AIA)** | ❌ | Entity + UI |
| PRJ-FR-07 **Subcontractors** | ❌ | Entity + UI |
| PRJ-FR-08 Cost Allocation events → Finance | ❌ | ربط ProjectId في Journal Lines |
| PRJ-FR-09 Revenue Recognition (IFRS 15) | ⚠️ Simplified | حساب بسيط على الـ Post |
| PRJ-FR-10 Completion Report | ❌ | PDF (لاحقاً) |

---

## 2. الأولويات (Phase 5.A → 5.B → 5.C)

### ✅ Phase 5.A — Finance Core (الأعلى قيمة)

| # | Component | Why |
|---|---|---|
| A1 | **Customers entity + AR** | Holding لازم فاتورة عميل |
| A2 | **Sales Invoices + Receipts** | تحصيل المبيعات (الـ AR الأساسي) |
| A3 | **Payments** | دفع الموردين/العملاء |
| A4 | **General Ledger Report** (per-account ledger) | التدقيق اليومي |
| A5 | **Balance Sheet + Cash Flow** | تقارير إلزامية |
| A6 | **Aging Reports (AR + AP)** | متابعة التحصيل |

### ✅ Phase 5.B — Projects Enhancement (مقاولات)

| # | Component | Why |
|---|---|---|
| B1 | **Customer link on Project** | ربط العميل بالمشروع |
| B2 | **Retainage field + compute** | خصم 5-10% من Progress Invoice |
| B3 | **Progress Invoice entity + UI** | AIA-style billing |
| B4 | **Subcontractors entity** | إدارة المقاولين الفرعيين |
| B5 | **Cost Center auto-create for Project** | ربط تلقائي عند الإنشاء |

### ⏸️ Phase 5.C — Modern (لاحقاً)

| # | Component | Note |
|---|---|---|
| C1 | Fixed Assets + Depreciation | يحتاج خطة منفصلة |
| C2 | Multi-Currency | أساس LYD فقط MVP |
| C3 | Intercompany + Consolidation | كبير — Phase 6 |
| C4 | Budget vs Actual standalone | بسيط، بعد B |
| C5 | Bank Reconciliation CSV | بعد C1 |
| C6 | Audit Trail | cross-cutting concern |

---

## 3. Data Model (Phase 5.A + 5.B)

### Customers (module: AR or extend Finance)
```
customers (id, tenant_id, company_id, code, name, name_en, tax_id,
           email, phone, address, credit_limit, payment_terms_days, is_active)
```

### SalesInvoices (new module or part of Finance)
```
sales_invoices (id, tenant_id, company_id, customer_id, invoice_number,
                invoice_date, due_date, currency_code, exchange_rate,
                subtotal, tax_amount, total_amount, paid_amount, outstanding,
                status [Draft|Sent|PartiallyPaid|Paid|Overdue|Cancelled],
                notes, project_id?, created_by, posted_by, posted_at)
sales_invoice_lines (id, sales_invoice_id, line_number, description,
                      item_id?, quantity, unit_price, tax_rate, line_total)
```

### Receipts
```
receipts (id, tenant_id, company_id, customer_id, receipt_number,
          receipt_date, amount, currency_code, payment_method, notes,
          bank_account_id?, posted_at)
receipt_allocations (id, receipt_id, sales_invoice_id, amount_applied)
```

### Payments (unified for both AR and AP)
```
payments (id, tenant_id, company_id, party_type [Customer|Vendor],
          party_id, payment_number, payment_date, amount, currency_code,
          payment_method, bank_account_id?, notes, posted_at)
payment_allocations (id, payment_id, ref_type [SalesInvoice|VendorBill], ref_id, amount)
```

### ProjectProgressInvoices
```
project_progress_invoices (id, tenant_id, project_id, invoice_number,
                            period_from, period_to, percent_complete,
                            gross_amount, retainage_amount, net_amount,
                            retained_amount, status [Draft|Sent|PartiallyPaid|Paid],
                            due_date, sales_invoice_id?, posted_at)
```

### Subcontractors (new module)
```
subcontractors (id, tenant_id, project_id, code, name, specialty, contact,
                 contract_amount, is_active)
subcontract_progress (id, subcontractor_id, period_from, period_to,
                       work_completed, amount_billed, status, paid_at?)
```

### Add Project fields
- `customer_id?` — link to Customer entity
- `retainage_percent` (decimal 0–100, default 10)
- `auto_cost_center` (bool) — already implemented (CostCenter created)

### Add JournalLine
- `project_id?` — already nullable, just verify FK link works

---

## 4. Frontend Pages (Phase 5.A + 5.B)

### Finance submenu (under "المالية")
- ✅ **دليل الحسابات** `/finance/accounts` (existing)
- 🆕 **العملاء** `/finance/customers` (list + form)
- 🆕 **فواتير المبيعات** `/finance/sales-invoices` (list + form + detail)
- 🆕 **سندات القبض** `/finance/receipts` (list + form + alloc)
- 🆕 **المدفوعات** `/finance/payments` (list + form + alloc)
- 🆕 **القيود** `/finance/journal-entries` (list + form)
- 🆕 **دفتر الأستاذ** `/finance/general-ledger?accountId=&from=&to=` (per-account ledger)
- 🆕 **ميزان المراجعة** `/finance/trial-balance?asOfDate=` (already has Reports endpoint)
- 🆕 **قائمة الدخل** `/finance/income-statement?from=&to=` (already has Reports endpoint)
- 🆕 **الميزانية** `/finance/balance-sheet?asOfDate=` (needs endpoint)
- 🆕 **التدفقات النقدية** `/finance/cash-flow?from=&to=` (needs endpoint)
- 🆕 **أعمار الديون (AR)** `/finance/aging/ar`
- 🆕 **أعمار الديون (AP)** `/finance/aging/ap`

### Projects submenu (under "المشاريع")
- ✅ **المشاريع** `/projects` (existing)
- 🆕 **تفاصيل المشروع** `/projects/[id]` (tabs: Overview / Tasks / Resources / Progress Invoices / Subcontractors / Cost Center)
- 🆕 **مهام المشروع** `/projects/[id]/tasks`
- 🆕 **فواتير التقدم** `/projects/[id]/progress-invoices`
- 🆕 **المقاولين الفرعيين** `/projects/[id]/subcontractors`

### Total new pages: ~18 pages

---

## 5. Sidebar update (AppShell.tsx)

```
💰 المالية ▼
├── دليل الحسابات      /finance/accounts
├── العملاء            /finance/customers 🆕
├── فواتير المبيعات     /finance/sales-invoices 🆕
├── سندات القبض         /finance/receipts 🆕
├── المدفوعات          /finance/payments 🆕
├── القيود            /finance/journal-entries 🆕
├── التقارير ▼
│   ├── دفتر الأستاذ     /finance/general-ledger 🆕
│   ├── ميزان المراجعة    /finance/trial-balance 🆕
│   ├── قائمة الدخل       /finance/income-statement 🆕
│   ├── الميزانية         /finance/balance-sheet 🆕
│   ├── التدفقات النقدية  /finance/cash-flow 🆕
│   ├── أعمار الذمم AR    /finance/aging/ar 🆕
│   └── أعمار الذمم AP    /finance/aging/ap 🆕

📊 المشاريع ▼
├── المشاريع          /projects
├── تفاصيل المشروع     /projects/[id] 🆕
├── المهام             /projects/[id]/tasks 🆕
├── فواتير التقدم      /projects/[id]/progress-invoices 🆕
└── المقاولين الفرعيين  /projects/[id]/subcontractors 🆕
```

---

## 6. التنفيذ المرحلي

### Sprint 1: AR Foundation (~3-5 أيام)
- Customers entity + service + controller + APIs
- Customers list + form pages
- Sales Invoices entity + service + controller + APIs
- Sales Invoices list + form + detail pages
- Receipts entity + allocations
- Receipts list + form
- Aging Report for AR

### Sprint 2: AP + Payments + GL Reports (~3-5 أيام)
- Payments entity + unified AR/AP
- Payments list + form + allocation
- General Ledger Report (per-account)
- Balance Sheet (basic)
- Cash Flow (basic indirect)

### Sprint 3: Projects Enhancement (~3-4 أيام)
- Project.CustomerId field migration
- ProjectProgressInvoice entity + service + APIs
- Subcontractor entity + service + APIs
- Project detail page with tabs
- Progress Invoice form (with Retainage auto-calc)

### Stretch (if time):
- Income Statement enhancement (department breakdown)
- Auto-create Customer Statement PDF
- Multi-currency basic LYD-only mode

---

## 7. Migration Plan

```csharp
// 20260701_120000_CreateARTables.cs
Create.Table("customers")...
Create.Table("sales_invoices")...
Create.Table("sales_invoice_lines")...

// 20260701_130000_CreatePaymentsAndReceiptsTables.cs
Create.Table("receipts")...
Create.Table("receipt_allocations")...
Create.Table("payments")...
Create.Table("payment_allocations")...

// 20260701_140000_ExtendProjectsForContracting.cs
Alter.Table("projects").AddColumn("customer_id").AsGuid().Nullable()
    .ForeignKey("fk_projects_customer", "customers", "id").OnDelete(Rule.SetNull);
Alter.Table("projects").AddColumn("retainage_percent").AsDecimal(8,4).Nullable();

// 20260701_150000_CreateProjectProgressInvoices.cs
Create.Table("project_progress_invoices")...

// 20260701_160000_CreateSubcontractorsTables.cs
Create.Table("subcontractors")...
Create.Table("subcontract_progress")...
```

---

## 8. ماخدناه من FRD + ما تم تعديله:

| FRD Item | قرارنا |
|---|---|
| R-3 Master CoA + Override | ✅ Master CoA عندنا (47 حساب)، **Override نتجاهله الآن** |
| FO-6 Arabic + Hijri + Arabic numerals | **نعتمد ميلادي + English numerals** (نماشياً مع user) |
| AR Customer Master | كل المعلومات المطلوبة ✅ (تنقص فقط name_en) |
| AR Receipts allocation | ✅ multi-invoice payment |
| AR Aging Buckets | ✅ 0-30, 31-60, 61-90, 91-120, 120+ |
| AP Bill workflow | ✅ basic + extension for Payment |
| Projects Retainage + Progress Invoice | ✅ fields + entity |
| Subcontractors | ✅ entity separate |
| Cost Center auto-create | ✅ already exists |
| IFRS 15 Performance Obligation | **simplified**: revenue at invoice posting (not % complete) |
| Intercompany + Consolidation | **مؤجل** Phase 6+ |
| Tax (Tx-FR-*) | **مؤجل** بطلب المستخدم |
| Manufacturing (Mfg-FR-*) | **خارج النطاق** (لا تخص المقاولات) |
| HR + Payroll in detail | **خارج النطاق** (في FRD paragraph 1.2) |
| Mobile Apps | **خارج النطاق** (next release) |
| PDF + Excel export | **مؤجل Phase 6** (noble priorty) |

---

## 9. Acceptance Criteria (Phase 5)

- [ ] Backend: dotnet build → 0 errors
- [ ] Backend: Smoke test for new endpoints (10+)
- [ ] AlFajr data: optionally seed Customers + Sales Invoices for demo
- [ ] Frontend: npm run build → 0 errors
- [ ] Frontend: 18 new pages render without crash
- [ ] End-to-end: Login → Create Customer → Create Invoice → Receive Payment → GL Report → Aging
- [ ] End-to-end: Login → Create Project → Add Customer → Create Progress Invoice (with Retainage) → GL posted

---

## 10. ملاحظة للـ UI/UX

- **System font**: Noto Naskh Arabic أو Tajawal (R-9)
// Currently we use system fonts; user reported '????' for Arabic in screenshot.
// Action: Add to `globals.css` font-family fallbacks:
// ```css
// body { font-family: 'Noto Naskh Arabic', 'Tajawal', system-ui, ... }
// ```

- **Dates**: keep en-GB locale (per user)
- **Numbers**: keep English digits (per user)
- **Currency**: LYD 123,456.789 د.ل (Arabic) — needs `.LYDFormatter()` helper in lib/utils.ts
