# 🛒 src/backend/Modules/Procurement/AGENTS.md

> Procurement Module — ✅ Phase 3 (Procurement Core: Vendor + PO + GR + Bill)

## شو فيه

```
Procurement/
├── Entities/
│   ├── Vendor.cs                # Vendor + PaymentTerms
│   ├── PurchaseOrder.cs         # PO Aggregate Root (Draft → ... → Received)
│   ├── GoodsReceipt.cs          # GR Aggregate Root (Draft → Received)
│   └── VendorBill.cs            # VB Aggregate Root (Draft → Posted)
├── Application/
│   ├── Dtos.cs                  # Request/Response DTOs
│   ├── Validators.cs            # FluentValidation (8 validators)
│   └── Services/
│       ├── VendorService.cs              # CRUD
│       ├── PurchaseOrderService.cs       # Create + Approve + Send
│       ├── GoodsReceiptService.cs        # Create + Receive (ينشئ StockMovement تلقائياً)
│       └── VendorBillService.cs          # Create + Post
└── Infrastructure/
    ├── IRepositories.cs         # IVendor/PurchaseOrder/GoodsReceipt/VendorBillRepository
    ├── VendorRepository.cs
    ├── PurchaseOrderRepository.cs
    ├── GoodsReceiptRepository.cs
    ├── VendorBillRepository.cs
    └── DocumentSequenceRepository.cs  # عداد PO/GR/Bill numbers
```

## Business Rules

### Purchase Order Workflow
```
Draft → Pending → Approved → Sent → Received
   ↓        ↓        ↓
Cancelled ← Cancelled
```

- **Create** → status = `Draft`، توليد PO Number تلقائي (`PO-2026-0001`)
- **Approve** → `Draft`/`Pending` → `Approved` (يتطلب صلاحية ProcurementManager — مستقبلي)
- **Send** → `Approved` → `Sent` (يرسل بريد إلكتروني للمورّد — مستقبلي)
- **Receive** → يحدث تلقائياً عند `Receive` للـ GR

### Goods Receipt
- **يُنشأ فقط لـ PO في حالة `Approved` أو `Sent`**
- الكمية المُستلمة **لا تتجاوز** الكمية في الـ PO (validation صارم)
- عند `Receive`:
  1. لكل بند: ينشئ `StockMovement.Receive` (Draft) ثم `PostAsync`
  2. `PostAsync` يحدّث `stock_levels` (moving weighted average) + ينشر `StockReceivedEvent`
  3. Finance handler يستقبل الـ event → ينشئ JournalEntry (Dr Inventory / Cr A/P)
  4. PO → `Received`

### Vendor Bill
- **يُنشأ فقط لـ GR في حالة `Received`**
- عند `Post` (Draft → Posted): نُحدّث الحالة + `PostedAt` (JournalEntry التفصيلي قادم في Phase 3.1)

## Document Numbering

`IDocumentSequenceRepository` ينشئ جدول `procurement_document_sequences` تلقائياً ويستخدم UPSERT لتوليد أرقام تسلسلية:
- `PO-2026-0001`, `PO-2026-0002`, ...
- `GR-2026-0001`, `GR-2026-0002`, ...
- `BILL-2026-0001`, `BILL-2026-0002`, ...

## Endpoints (16)

| Method | Path | الوصف |
|--------|------|-------|
| GET | `/api/procurement/vendors` | قائمة المورّدين |
| GET | `/api/procurement/vendors/{id}` | تفاصيل مورّد |
| POST | `/api/procurement/vendors` | إنشاء مورّد |
| PUT | `/api/procurement/vendors/{id}` | تحديث مورّد |
| DELETE | `/api/procurement/vendors/{id}` | soft-delete (IsActive=false) |
| GET | `/api/procurement/pos` | قائمة POs + filter (vendor, status) |
| GET | `/api/procurement/pos/{id}` | تفاصيل PO + lines |
| POST | `/api/procurement/pos` | إنشاء PO (Draft) |
| PUT | `/api/procurement/pos/{id}/approve` | موافقة (Draft/Pending → Approved) |
| PUT | `/api/procurement/pos/{id}/send` | إرسال للمورّد (Approved → Sent) |
| GET | `/api/procurement/grs` | قائمة GRs + filter (po, status) |
| GET | `/api/procurement/grs/{id}` | تفاصيل GR + lines |
| POST | `/api/procurement/grs` | إنشاء GR (Draft) |
| PUT | `/api/procurement/grs/{id}/receive` | تأكيد استلام (Draft → Received) |
| GET | `/api/procurement/bills` | قائمة Bills + filter (vendor, gr, status) |
| GET | `/api/procurement/bills/{id}` | تفاصيل Bill + lines |
| POST | `/api/procurement/bills` | إنشاء Bill (Draft) |
| PUT | `/api/procurement/bills/{id}/post` | ترحيل (Draft → Posted) |

## لما تشتغل هنا

- **إضافة صلاحية على workflow**: استخدم `[Authorize(Roles="ProcurementManager")]` على approve/send
- **إضافة PDF/Email**: عند Send → أنشئ PDF + أرسل بريد إلكتروني
- **VendorBill → JournalEntry**: أضف `IIntegrationEventHandler<VendorBillPostedEvent>` + PostingRule جديد
- **إضافة GR مع multiple POs**: عدّل الـ entity (الآن 1:1)
- **إضافة Landed Cost**: أضف cost components على GR (Phase 4)

## بعد التعديل

- شغّل `dotnet build` → 0 errors
- `dotnet test` → كل التيستات الموجودة يجب أن تنجح
- إذا غيّرت workflow status: حدّث الـ validTransitions في `PurchaseOrderService` + Validators

## تكامل مع الموديولات الأخرى

- **Inventory** (Phase 2.2-2.3): GR.Receive ينشئ StockMovement تلقائياً عبر IStockMovementService
- **Finance** (Phase 1): StockReceivedEvent (الموجود) ينشئ JournalEntry تلقائياً
  - Dr Inventory (1300) / Cr Accounts Payable (2100)
- **Companies** (Phase 1.5): مستقبلي — PO و GR سيحملان `company_id` (حالياً Guid.Empty)

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Inventory/AGENTS.md`](../Inventory/AGENTS.md) — StockMovement integration
- [`../Finance/AGENTS.md`](../Finance/AGENTS.md) — JournalEntry creation via events
- [`../../Host/AGENTS.md`](../../Host/AGENTS.md) — DI registration


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
