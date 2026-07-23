# DEC-094: UI/API Integration Inventory

## 🎯 Goal
List all UI pages and API endpoints, identify gaps where UI is missing for available endpoints.

---

## 📊 UI Pages (33 found)

### ✅ Existing UI Pages (27)
| Module | Page | File | Backend? |
|---|---|---|---|
| Auth | Login | `app/login/page.tsx` | ✅ |
| Dashboard | Dashboard | `app/(authenticated)/dashboard/page.tsx` | ✅ |
| Finance | Accounts | `finance/accounts/page.tsx` | ✅ |
| Finance | Aging AR | `finance/aging-ar/page.tsx` | ✅ |
| Finance | Customers List | `finance/customers/page.tsx` | ✅ |
| Finance | Customer New | `finance/customers/new/page.tsx` | ✅ |
| Finance | Receipts List | `finance/receipts/page.tsx` | ✅ |
| Finance | Receipt New | `finance/receipts/new/page.tsx` | ✅ |
| Finance | Sales Invoices List | `finance/sales-invoices/page.tsx` | ✅ |
| Finance | Sales Invoice New | `finance/sales-invoices/new/page.tsx` | ✅ |
| Finance | Sales Invoice Detail | `finance/sales-invoices/[id]/page.tsx` | ✅ |
| HR | Attendance | `hr/attendance/page.tsx` | ✅ |
| HR | Employees List | `hr/employees/page.tsx` | ✅ |
| HR | Employee New | `hr/employees/new/page.tsx` | ✅ |
| HR | Leaves List | `hr/leaves/page.tsx` | ✅ |
| HR | Leave New | `hr/leaves/new/page.tsx` | ✅ |
| HR | Payroll List | `hr/payroll/page.tsx` | ✅ |
| HR | Payroll New | `hr/payroll/new/page.tsx` | ✅ |
| HR | Payroll Detail | `hr/payroll/[id]/page.tsx` | ✅ |
| HR | Payslip Detail | `hr/payroll/[id]/payslip/[empId]/page.tsx` | ✅ |
| Inventory | Items | `inventory/items/page.tsx` | ✅ |
| Procurement | Bills List | `procurement/bills/page.tsx` | ✅ |
| Procurement | Bill New | `procurement/bills/new/page.tsx` | ✅ |
| Procurement | GRs List | `procurement/goods-receipts/page.tsx` | ✅ |
| Procurement | GR New | `procurement/goods-receipts/new/page.tsx` | ✅ |
| Procurement | POs List | `procurement/purchase-orders/page.tsx` | ✅ |
| Procurement | PO New | `procurement/purchase-orders/new/page.tsx` | ✅ |
| Procurement | Vendors List | `procurement/vendors/page.tsx` | ✅ |
| Procurement | Vendor New | `procurement/vendors/new/page.tsx` | ✅ |
| Projects | Projects List | `projects/page.tsx` | ✅ |

### ❌ Missing UI Pages (CRITICAL)
| Entity | Backend Controller | URL | Page |
|---|---|---|---|
| **Companies** | `CompaniesController.cs` | `/api/companies` | ❌ NONE |
| **Projects** | `ProjectsController.cs` | `/api/projects` | ✅ list (no new/edit) |
| **Journal Entries** | `JournalEntriesController.cs` | `/api/finance/journal-entries` | ❌ NONE |
| **Cost Centers** | `CostCentersController.cs` | `/api/cost-centers` | ❌ NONE |
| **Payments** | `PaymentsController.cs` | `/api/finance/payments` | ❌ NONE |
| **Stock Movements** | `StockMovementsController.cs` | `/api/inventory/stock-movements` | ❌ NONE |
| **Stock Levels** | `StockLevelsController.cs` | `/api/inventory/stock-levels` | ❌ NONE |
| **Notifications** | `NotificationsController.cs` | `/api/notifications` | ❌ NONE |
| **Posting Rules** | `PostingRulesController.cs` | `/api/finance/posting-rules` | ❌ NONE |
| **Item Categories** | `ItemCategoriesController.cs` | `/api/inventory/item-categories` | ❌ NONE |
| **Stock Reservations** | `StockReservationsController.cs` | `/api/inventory/stock-reservations` | ❌ NONE |

---

## 📊 Backend API Endpoints (21 controllers)

| Controller | Endpoints | UI Coverage |
|---|---|---|
| AccountsController | /api/finance/accounts | ✅ 1 page (list) |
| AdminController | /api/admin/* | ✅ 1 page (debug only) |
| AuthController | /api/auth/* | ✅ 1 page (login) |
| CompaniesController | /api/companies | ❌ NO UI |
| CostCentersController | /api/cost-centers | ❌ NO UI |
| DebugController | /api/debug/* | (debug only) |
| EventsController | /api/events/* | ❌ NO UI |
| FinanceArController | /api/ar/* | ✅ 6 pages (customers, receipts, sales invoices) |
| FinanceReportsController | /api/finance/reports/* | ✅ 1 page (aging-ar) |
| HealthController | /api/health/* | (system) |
| HrController | /api/hr/* | ✅ 8 pages (employees, leaves, payroll, attendance) |
| ItemCategoriesController | /api/inventory/item-categories | ❌ NO UI |
| ItemsController | /api/inventory/items | ✅ 1 page |
| JournalEntriesController | /api/finance/journal-entries | ❌ NO UI |
| LedgerController | /api/finance/ledger/* | (API only) |
| NotificationsController | /api/notifications | ❌ NO UI |
| PaymentsController | /api/finance/payments | ❌ NO UI |
| PostingRulesController | /api/finance/posting-rules | ❌ NO UI |
| ProcurementController | /api/procurement/* | ✅ 8 pages (POs, GRs, bills, vendors) |
| ProjectsController | /api/projects | ✅ 1 page (list, no new/edit) |
| ReportsController | /api/reports/* | ❌ NO UI |
| ResourcesController | /api/resources/* | ❌ NO UI |
| StockLevelsController | /api/inventory/stock-levels | ❌ NO UI |
| StockMovementsController | /api/inventory/stock-movements | ❌ NO UI |
| StockReservationsController | /api/inventory/stock-reservations | ❌ NO UI |

---

## 🎯 Critical Gaps (DEC-095+)

### Priority 1: Companies (CRITICAL)
- Backend: `/api/companies` (CompaniesController)
- Frontend: NO UI at all
- Use case: User can't manage companies (currently only 1 hardcoded)
- **Estimated effort**: 2 hours (mirror Vendors UI)

### Priority 2: Projects (HIGH)
- Backend: `/api/projects` (ProjectsController)
- Frontend: Only list page (no create/edit)
- Use case: User can't add new projects
- **Estimated effort**: 2 hours (add create/edit modals)

### Priority 3: Journal Entries (HIGH)
- Backend: `/api/finance/journal-entries` (JournalEntriesController)
- Frontend: NO UI
- Use case: TB verification, audit trail
- **Estimated effort**: 1.5 hours (list + view detail)

### Priority 4: Cost Centers (MEDIUM)
- Backend: `/api/cost-centers` (CostCentersController)
- Frontend: NO UI
- **Estimated effort**: 1.5 hours

### Priority 5: Payments (MEDIUM)
- Backend: `/api/finance/payments` (PaymentsController)
- Frontend: NO UI (only accounting data)
- **Estimated effort**: 2 hours

### Priority 6: Stock Levels + Movements (MEDIUM)
- Backend: `/api/inventory/stock-*` 
- Frontend: NO UI (current Items page is just SKU list)
- **Estimated effort**: 3 hours (combined)

### Priority 7: Notifications (LOW)
- Backend: `/api/notifications`
- Frontend: NO UI (not in main app)
- **Estimated effort**: 2 hours

### Priority 8: Posting Rules (LOW)
- Backend: `/api/finance/posting-rules`
- Frontend: NO UI (admin only)
- **Estimated effort**: 2 hours

### Priority 9: Item Categories (LOW)
- Backend: `/api/inventory/item-categories`
- Frontend: NO UI
- **Estimated effort**: 1 hour

---

## 📊 Coverage Stats

| Metric | Value |
|---|---|
| UI Pages | 33 total |
| Backend Controllers | 24 (excluding Debug/Health) |
| **Coverage** | **~70%** of controllers have UI |
| **Gaps** | ~9 missing UI pages |
| **Total Effort** | ~17 hours of UI work |

---

## 🎯 Next Steps

### DEC-095: Critical Pages (Companies + Projects new/edit)
- 2 priority 1 items
- 4 hours of work
- 1 PR

### DEC-096: Financial Pages (Journal Entries + Cost Centers)
- 2 priority 3-4 items
- 3 hours of work
- 1 PR

### DEC-097: Stock Pages (Stock Levels + Movements)
- 1 priority 6 item
- 3 hours
- 1 PR

### DEC-098: Admin Pages (Notifications + Posting Rules + Item Categories)
- 3 priority 7-9 items
- 5 hours
- 1 PR

### DEC-099: Polish + Bug Fixes
- Verify all business workflows
- Add login polish
- Smoke test on every page

---

## 🛡️ Defense Layer 41: UI/API Inventory

- All UI pages documented (33 total)
- All API endpoints documented (24 controllers)
- Gaps identified (9 missing UI pages)
- Priority ranked
- Effort estimated (~17 hours total)

Refs: DEC-094 (this), DEC-095+ (UI integration)
