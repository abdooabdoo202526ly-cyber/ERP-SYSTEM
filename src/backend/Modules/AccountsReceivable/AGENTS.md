# 💼 src/backend/Modules/AccountsReceivable/AGENTS.md

> AccountsReceivable Module — ✅ Phase 5 Sprint 1 (FM-2 AR — ذمم مدينة)
>
> محدّث: 2026-07-01 — Sprint 1 deliverable: Customers + SalesInvoices + Receipts + Aging

## Phase 5 Update (2026-07-01)

### الكيانات الجديدة (5 جداول)

| Entity | الغرض |
|--------|-------|
| `Customer` | ماستر العملاء (code فريد لكل tenant) + credit limit + paymentTermsDays |
| `SalesInvoice` + `SalesInvoiceLine` | فواتير المبيعات (Draft → Sent → PartiallyPaid/Paid → Cancelled) |
| `Receipt` + `ReceiptAllocation` | سندات القبض مع تخصيص متعدد على الفواتير |

### Endpoints (15+)

| Method | Path | الوصف |
|--------|------|-------|
| GET | `/api/ar/customers` | قائمة العملاء (paginated) |
| GET | `/api/ar/customers/{id}` | تفاصيل عميل |
| POST | `/api/ar/customers` | إنشاء عميل |
| PUT | `/api/ar/customers/{id}` | تحديث عميل |
| DELETE | `/api/ar/customers/{id}` | soft-delete (IsActive=false) |
| GET | `/api/ar/sales-invoices` | قائمة الفواتير (filter بـ customer/status) |
| GET | `/api/ar/sales-invoices/{id}` | تفاصيل فاتورة + Lines + Allocations |
| POST | `/api/ar/sales-invoices` | إنشاء فاتورة (optionally postImmediately) |
| PUT | `/api/ar/sales-invoices/{id}` | تحديث (Draft فقط) |
| PUT | `/api/ar/sales-invoices/{id}/post` | ترحيل (Draft → Sent + JournalEntry) |
| PUT | `/api/ar/sales-invoices/{id}/cancel` | إلغاء |
| GET | `/api/ar/receipts` | قائمة السندات (filter بـ customer) |
| GET | `/api/ar/receipts/{id}` | تفاصيل سند + Allocations |
| POST | `/api/ar/receipts` | إنشاء سند (optionally postImmediately) |
| PUT | `/api/ar/receipts/{id}/post` | ترحيل (Dr 1210 / Cr 1230) |
| PUT | `/api/ar/receipts/{id}/reverse` | عكس السند (قيد عكسي) |
| GET | `/api/ar/aging?asOfDate=` | تقرير أعمار الذمم (5 buckets) |

### Conventions المعتمدة

- **Modular Monolith pattern** — Entities/Application/Infrastructure (مثل Procurement)
- **Dapper + raw SQL** (لا EF Core) — Decision في root AGENTS.md
- **Multi-tenancy** — كل query يفلتر بـ `tenant_id`
- **Status enum → string** في DB (مثل Procurement) — EnumStringTypeHandler يحوّل عند القراءة
- **FluentValidation** — كل Request DTO له Validator ينتهي بـ "Validator"
- **Result pattern** — `ArResult<T>` موحد عبر الـ module
- **DocumentSequence** — جدول `ar_document_sequences` للـ SI/RC numbers
- **JournalEntry integration** — يستخدم `IJournalEntryService` + `IAccountRepository` من Finance module
- **Decimal precision**: decimal(18,4) للمبالغ، decimal(18,8) لسعر الصرف

### الحسابات المحاسبية (CoA seed)

| الحساب | الكود | الاستخدام |
|--------|------|-----------|
| ذمم مدينة (عملاء خارجيين) | 1230 | Dr عند Post فاتورة / Cr عند Post سند قبض |
| إيرادات المشاريع | 5110 | Cr عند Post فاتورة |
| النقدية | 1210 | Dr عند Post سند قبض |

**Fallback chain** في الكود: `1230 ?? 1220` للذمم؛ `5110 ?? 4100` للإيرادات؛ `1210` فقط للنقدية.

### بنية الـ Module

```
AccountsReceivable/
├── Entities/
│   ├── Customer.cs              # Customer
│   ├── SalesInvoice.cs          # SalesInvoice + SalesInvoiceStatus + SalesInvoiceLine + PaymentMethod
│   └── Receipt.cs               # Receipt + ReceiptAllocation
├── Application/
│   ├── Dtos.cs                  # كل DTOs (Customer / SalesInvoice / Receipt / Aging)
│   ├── Validators.cs            # 7 FluentValidation rules
│   └── Services/
│       ├── CustomerService.cs           # ICustomerService + ArResult<T>
│       ├── SalesInvoiceService.cs       # ISalesInvoiceService + Post→JournalEntry
│       └── ReceiptService.cs            # IReceiptService + Post + Reverse
└── Infrastructure/
    ├── IRepositories.cs         # ICustomer / ISalesInvoice / IReceipt / IArDocumentSequence
    ├── CustomerRepository.cs
    ├── SalesInvoiceRepository.cs        # يحسب Outstanding عبر SQL (total-paid)
    ├── ReceiptRepository.cs
    └── ArDocumentSequenceRepository.cs
```

### Business Rules

#### SalesInvoice Workflow
```
Draft → Sent → PartiallyPaid → Paid
   ↓        ↓                    ↓
Cancelled  Cancelled (لا يمكن لو paidAmount > 0)
```

- **Create** → status = `Draft`، توليد `SI-YYYY-NNNNNN` تلقائي (DapperSequence)
- **Post** → ينشئ JournalEntry (Dr 1230 / Cr 5110) — Reference = `SI:{id}`، ثم status = `Sent`
- **RecordPayment** (عبر Receipt.Post + allocations) → يحدّث `paidAmount` + `status`
- **Cancel** → فقط لو `paidAmount = 0`
- **Outstanding** يُحسب في الـ repository: `(total_amount - paid_amount) AS Outstanding`

#### Receipt Workflow
```
Draft → Posted → (Reversed)
```

- **Create** → status = `Draft`، توليد `RC-YYYY-NNNNNN` تلقائي
- **Validation** — مجموع Allocations يجب أن يساوي Amount بالضبط
- **Post** → ينشئ JournalEntry (Dr 1210 / Cr 1230) + يحدّث SalesInvoice.paidAmount و status
- **Reverse** → ينشئ JournalEntry عكسي (Dr 1230 / Cr 1210) + يسترجع مبالغ الفواتير
- **Status transitions**:
  - invoice.paidAmount == total → `Paid`
  - invoice.paidAmount > 0 → `PartiallyPaid`
  - invoice.paidAmount == 0 → `Sent` (للمرحّلة) أو `Draft` (للمسودة)

#### Aging Buckets
- 0-30 يوم (current + daysPast <= 30)
- 31-60 يوم
- 61-90 يوم
- 91-120 يوم
- 120+ يوم (متأخر جداً)
- يحسب daysPast = `asOfDate - DueDate` (أو invoiceDate لو DueDate null)

## لما تشتغل هنا

- **إضافة Tax fields**: عدّل `SalesInvoice` و `Receipt` DTOs (مؤجل حالياً)
- **Customer Statement PDF**: أنشئ خدمة جديدة + endpoint
- **Credit Note**: أضف `CreditNote` entity + workflow منفصل (يستعكس invoice)
- **Multi-currency AR**: أضف FX gain/loss عند Post لو CurrencyCode != LYD
- **Project customer link**: املأ `projectId` (الـ FK موجود بالفعل، الـ UI لسه)

## بعد التعديل

- `dotnet build` → 0 errors (مُتحقَّق — 2026-07-01)
- `npm run build` → 0 errors (مُتحقَّق — 2026-07-01)
- Smoke test: login → create customer → create+post invoice (JE) → create+post receipt → aging → ✅
- حدّث AGENTS.md هذا عند أي تغيير في workflow أو status

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md) — backend overview
- [`../Finance/AGENTS.md`](../Finance/AGENTS.md) — JournalEntry + CoA accounts 1210/1230/5110
- [`../Procurement/AGENTS.md`](../Procurement/AGENTS.md) — النمط المُتبع (Vendor/PO/GR/Bill)
- [`../Payroll/AGENTS.md`](../Payroll/AGENTS.md) — Fallback chain pattern للحسابات
- [`../../Host/AGENTS.md`](../../Host/AGENTS.md) — DI registration
- [`../../Shared/AGENTS.md`](../../Shared/AGENTS.md) — Migrations (010_CreateARTables)
- [`docs/PHASE-5-FINANCE-PROJECTS-PLAN.md`](../../../../docs/PHASE-5-FINANCE-PROJECTS-PLAN.md) — Sprint 1


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
