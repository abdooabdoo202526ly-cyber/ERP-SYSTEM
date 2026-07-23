# 💰 Payments Module (Phase 5.A)

> سندات الدفع والقبض الموحّدة — تخدم AP (Vendor) و AR (Customer، مُستقبلي).
>
> محدّث: 2026-07-01 — Phase 5.A Sprint 2

## شو فيه

```
Payments/
├── Entities/
│   └── Payment.cs            # Payment, PaymentAllocation + enums (PaymentStatus, PaymentPartyTypes, PaymentMethods, PaymentRefTypes)
├── Application/
│   ├── PaymentDtos.cs        # CreatePaymentRequest, AllocatePaymentRequest, PaymentResponse, PaymentResult<T>, PaymentErrorCode
│   ├── Validators.cs         # CreatePaymentRequestValidator, AllocatePaymentRequestValidator
│   └── Services/
│       └── PaymentService.cs # Create + GetById + List + Post + Allocate (ينشئ JournalEntry 2-leg عند Post)
└── Infrastructure/
    ├── IPaymentRepository.cs
    ├── PaymentRepository.cs  # Dapper (snake_case) + multi-tenant filter
    └── PaymentSequenceRepository.cs  # PAY-YYYY-NNNN sequence (يُعيد استخدام procurement_document_sequences)
```

## Domain Model

### Payment
- `partyType`: "Vendor" | "Customer" (مفتوح — يدعم كلا الـ streams)
- `partyId`: Guid → Vendor حالياً (Procurement)، Customer مُستقبلي
- `paymentNumber`: تسلسلي عبر `procurement_document_sequences` (prefix "PAY") → `PAY-2026-0001`
- `status`: Draft (1) → Posted (2) → Cancelled (3)
- `journalEntryId`: ربط بالقيد المُنشأ عند Post

### PaymentAllocation
- `refType`: "VendorBill" | "SalesInvoice"
- `refId`: Guid → vendor_bill.id أو sales_invoice.id (مستقبلي)
- `amountApplied`: decimal(18,4)
- **On Account semantics:** sum(allocations) ≤ amount. الفرق = "دفعة مقدمة".

## Posting Logic (داخل PaymentService.PostAsync)

عند Post، يُنشأ JournalEntry 2-line:

| PartyType | Dr (مدين) | Cr (دائن) | الوصف |
|-----------|-----------|-----------|--------|
| **Vendor** (AP) | 2210 (دائنون لموردين) | 1210 (النقدية) | سداد فاتورة مورّد |
| **Customer** (AR) | 1210 (النقدية) | 1230 (ذمم مدينة) | رد/قبض من عميل |

> الـ Dr/Cr يُحدّدان من كود الحساب في الـ CoA (الافتراضي موجود في DefaultCoASeed).

## Endpoints (4)

| Method | Path | الوصف |
|--------|------|--------|
| GET | `/api/payments` | قائمة السندات (filter بـ partyType/partyId/status + pagination) |
| GET | `/api/payments/{id}` | تفاصيل سند + allocations |
| POST | `/api/payments` | إنشاء مسودة (Draft) + allocations اختيارية |
| POST | `/api/payments/{id}/post` | ترحيل → ينشئ JournalEntry ويحدّث الحالة |
| POST | `/api/payments/{id}/allocate` | تخصيصات إضافية على سند مُرحَّل |

## Constraints / Rules

- **Party validation:** عند Vendor → يفحص IVendorRepository.GetByIdAsync. عند Customer → يرفض (الكيان غير موجود بعد).
- **Allocation cap:** sum(allocations) ≤ amount. `OverAllocation` error إن زاد.
- **Outstanding check:** لكل VendorBill allocation، يفحص outstanding (totalAmount - paid) ويرفض لو amountToApply > outstanding + 0.0001.
- **Status flow:** Draft → Posted (one-way). لا يمكن تخصيص (Allocate) إلا على Posted.
- **مستقبلي:** عند إضافة Customers entity، يصبح Customer path نشط.

## لما تشتغل هنا

- **Add Customer path:** أنشئ ICustomerRepository، استبدل `NotFoundParty` بـ vendor/customer check في PaymentService.
- **Multi-currency:** حالياً currency_code للعرض فقط. لتفعيل FX، أضف سعر الصرف + ربط بفروقات العملة.
- **Bank accounts:** BankAccountId nullable. عند إضافة BankAccount entity، تحقق من رصيد الحساب قبل Post.

## بعد التعديل

- `dotnet build` → 0 errors
- `dotnet test` → كل التيستات الموجودة (114) يجب أن تنجح + الـ 7 الجديدة
- حدّث AGENTS.md هذا عند إضافة entity / endpoint جديد

## DI Registration

```csharp
// في Program.cs
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentSequenceRepository, PaymentSequenceRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreatePaymentRequestValidator>();
```

## Migration

`20260701_130000_CreatePaymentsTables.cs`:
- `payments` (header) — 19 أعمدة + 4 indexes
- `payment_allocations` — 6 أعمدة + 3 indexes + FK إلى payments (CASCADE) + FK إلى journal_entries (SET NULL)

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md) — root
- [`../Finance/AGENTS.md`](../Finance/AGENTS.md) — يستهلك journal_entries + accounts (للترحيل)
- [`../Procurement/AGENTS.md`](../Procurement/AGENTS.md) — يستهلك vendor_bills + vendors (للـ AP)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
