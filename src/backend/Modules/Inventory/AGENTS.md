# 📦 src/backend/Modules/Inventory/AGENTS.md

> Inventory Module — ✅ Phase 2.2 + 2.3 (Core + Stock Movements + Notifications).
>
> محدّث: 2026-06-24 — إضافة Phase 3+ context

## شو فيه

```
Inventory/
├── Entities/
│   ├── Item.cs              # Item + ItemType + CostingMethod
│   ├── Warehouse.cs
│   ├── UnitOfMeasure.cs
│   ├── ItemCategory.cs
│   ├── StockMovement.cs     # Aggregate Root (Draft → Posted → Reversed)
│   ├── StockLevel.cs        # CQRS Read Model (denormalized)
│   └── StockReservation.cs  # holds for projects/orders
├── Application/
│   ├── InventoryDtos.cs            # Items, Warehouses, UoM, Category
│   ├── StockMovementDtos.cs        # Receive, Issue, Transfer, Adjust, Reservations
│   ├── Validators.cs
│   ├── StockMovementValidators.cs
│   └── Services/
│       ├── InventoryServices.cs             # Item, Warehouse, UoM, Category
│       ├── StockMovementService.cs          # CQRS-lite: PostAsync updates stock_levels
│       ├── SupportingStockServices.cs       # Level, Reservation
│       └── InventoryBootstrapper.cs         # seeds on tenant create
└── Infrastructure/
    ├── IRepositories.cs
    ├── ItemRepository.cs
    ├── WarehouseRepository.cs
    ├── UnitOfMeasureRepository.cs
    ├── ItemCategoryRepository.cs
    ├── IRepositories2.cs
    ├── StockMovementRepository.cs
    ├── StockLevelRepository.cs          # UPSERT + optimistic version
    └── StockReservationRepository.cs

../Notifications/   (new module)
├── Entities/Notification.cs
├── Infrastructure/NotificationRepository.cs
└── Application/Services/NotificationService.cs
```

## CQRS-lite Pattern (Phase 2.3)

### Write Side (StockMovement)
1. **Insert as Draft** (`CreateReceive/Issue/Transfer/Adjust`)
2. **PostAsync** (single transaction):
   - Status: Draft → Posted
   - ApplyToStockLevel (UPSERT with weighted moving average)
   - For Transfer: 2 levels (source -, dest +)
   - **LowStock check**: if `item.ReorderLevel > 0 AND level.QuantityAvailable < ReorderLevel` → create Notification

### Moving Weighted Average (Receive)
```
newAvg = (oldQty * oldAvg + qty * unitCost) / newQty
```

### Reverse
- Creates opposite Posted movement with `ReversedByMovementId` link
- Marks original as Reversed (audit trail preserved)

### Read Side (StockLevel)
- Denormalized on Post (synchronous, single-transaction)
- Future: async projection via MartenDB

## Stock Movement Types
| Type | Direction | Effect on stock |
|------|-----------|-----------------|
| Receive | + | OnHand += qty, AvgCost weighted |
| Issue | - | OnHand -= qty |
| Transfer | - (out) + (in) | 2 levels updated atomically |
| Adjust | +/- | Manual correction |
| Return | + | Customer return |

## Notifications (In-App)
- جدول `notifications` (لا email، لا push)
- Types: `"LowStock"` (الوحيد حالياً)
- Endpoints: `GET /api/inventory/notifications` و `/unread` و `POST .../mark-read`

## Endpoints (15)

| Method | Path | الـ Function |
|--------|------|-------------|
| GET/POST `/receive`/`/issue`/`/transfer`/`/adjust` | /api/inventory/movements | CRUD + draft |
| GET `/{id}` | | detail |
| POST `/{id}/post` | | Draft → Posted (CQRS) |
| POST `/{id}/reverse` | | عكس |
| GET | /api/inventory/levels/items/{itemId} | per-item |
| GET | /api/inventory/levels/warehouses/{warehouseId} | per-warehouse |
| GET | /api/inventory/levels/low-stock | low stock (joins items) |
| GET/POST/DELETE | /api/inventory/reservations | holds |
| GET | /api/inventory/notifications | user notifications |
| GET | /api/inventory/notifications/unread | unread with count |
| POST `/{id}/mark-read` | | mark as read |

## لما تشتغل هنا

- إضافة event لـ Posting: في `PostAsync` أضف handler يستدعي `INotificationService.CreateAsync`
- إضافة event integration: في PR #7 (Event Bus) — يستدعي Finance PostingRulesService
- تخصيص threshold: غيّر `item.ReorderLevel` و `item.ReorderQuantity` (PR #8 Reports للـ analytics)

## بعد التعديل

- شغّل `dotnet test` (13 tests جديد + 71 سابق = 84/84)
- إذا أضفت type جديد: عدّل `StockMovementType` enum + `ApplyToStockLevel` logic
- إذا غيّرت الـ low stock trigger: عدّل `StockMovementService.PostAsync` + tests

## تكامل مع الموديولات الأخرى

- **Finance** (Phase 1): PR #7 — StockMovement.PostAsync سيُنشئ Journal Entry
  (Inventory +/COGS - for Issue) عبر PostingRules
- **Projects** (Phase 2.1): `StockMovement.ProjectId` — ربط المخزون بمشروع
- **Reports** (Phase 2.5): Stock Valuation، Low Stock Reports، Movement History

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Identity/AGENTS.md`](../Identity/AGENTS.md)
- [`../Finance/AGENTS.md`](../Finance/AGENTS.md)
- [`../Companies/AGENTS.md`](../Companies/AGENTS.md)
- [`../Projects/AGENTS.md`](../Projects/AGENTS.md)
- [`../Notifications/AGENTS.md`](../Notifications/AGENTS.md)
- [`../Procurement/AGENTS.md`](../Procurement/AGENTS.md) — Phase 3 (Goods Receipts → Stock)
- [`../HR/AGENTS.md`](../HR/AGENTS.md) — Phase 3.5
- [`../Payroll/AGENTS.md`](../Payroll/AGENTS.md) — Phase 4
- [`../Procurement/AGENTS.md`](../Procurement/AGENTS.md) — Phase 3 (Goods Receipts)
- [`../HR/AGENTS.md`](../HR/AGENTS.md) — Phase 3.5
- [`../Payroll/AGENTS.md`](../Payroll/AGENTS.md) — Phase 4


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
