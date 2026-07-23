# 📊 src/backend/Modules/Reports/AGENTS.md

> Reports Module — ✅ Phase 2.5 (مكتمل).
>
> محدّث: 2026-06-24 — إضافة Phase 3+ context

## شو فيه

وحدة التقارير المالية والتشغيلية. تقرأ البيانات من DB عبر `IDbConnectionFactory` (Dapper) وتُرجع DTOs محسوبة. **لا تكتب** للـ DB — قراءة فقط (CQRS Read Side).

## Services (3) + 12 DTOs

| Service | Methods | DTOs |
|---------|---------|------|
| **FinanceReportService** | `GetTrialBalanceAsync`<br>`GetIncomeStatementAsync`<br>`GetBalanceSheetAsync` | `TrialBalanceReport`, `TrialBalanceRow`<br>`IncomeStatement`<br>`BalanceSheet` |
| **InventoryReportService** | `GetStockValuationAsync`<br>`GetMovementHistoryAsync`<br>`GetLowStockAsync`<br>`GetStockAgingAsync` | `StockValuation`<br>`StockMovementHistory`<br>`LowStockItem`<br>`StockAging` |
| **ProjectReportService** | `GetProjectPnLAsync`<br>`GetBudgetVsActualAsync`<br>`GetProjectsSummaryAsync` | `ProjectPnL`<br>`ProjectBudgetVsActual`<br>`ProjectSummary` |

## HTTP Endpoints (12)

كل الـ endpoints تحت `/api/reports` ومُتطلّبة `[Authorize]`:

### Project (3)
- `GET /projects/{id}/pnl?from=&to=` → ProjectPnL
- `GET /projects/{id}/budget-vs-actual` → ProjectBudgetVsActual
- `GET /projects/summary?companyId=` → list of ProjectSummary

### Inventory (4)
- `GET /inventory/valuation?companyId=&warehouseId=` → { count, totalValue, items }
- `GET /inventory/movements?itemId=&from=&to=&skip=&take=` → list of StockMovementHistory (paged 1-200, default 50)
- `GET /inventory/low-stock?companyId=` → list of LowStockItem
- `GET /inventory/aging?companyId=` → list of StockAging

### Finance (3)
- `GET /finance/trial-balance?companyId=&asOfDate=` → TrialBalanceReport
- `GET /finance/income-statement?companyId=&from=&to=` → IncomeStatement
- `GET /finance/balance-sheet?companyId=&asOfDate=` → BalanceSheet

## الـ DTO Computed Properties (مهمة للـ tests)

- `TrialBalanceRow.NetDebit` / `NetCredit` = `Debit - Credit` / `Credit - Debit`
- `TrialBalanceReport.IsBalanced` = `|TotalDebit - TotalCredit| < 0.01`
- `IncomeStatement.GrossProfit` = `Revenue - Cogs`
- `IncomeStatement.NetIncome` = `GrossProfit - OpEx + OtherIncome - OtherExpenses`
- `BalanceSheet.IsBalanced` = `|Assets - (Liab + Equity)| < 0.01`
- `StockValuation.TotalValue` = `QuantityOnHand * AverageCost`
- `LowStockItem.QuantityAvailable` = `QuantityOnHand - QuantityReserved` (computed في DB)
- `LowStockItem.Shortfall` = `ReorderLevel - QuantityAvailable`
- `LowStockItem.Status` = `"Critical"` if `QtyOnHand == 0`; else `"Warning"` if `QtyOnHand < ReorderLevel/2`; else `"Low"`
- `StockAging.AgeBucket` = `"0-30"` / `"31-60"` / `"61-90"` / `"90+"`
- `ProjectPnL.DirectCosts` = `MaterialCost + LaborCost + SubcontractorCost`
- `ProjectPnL.NetProfit` = `Revenue - DirectCosts - AllocatedOverhead`
- `ProjectBudgetVsActual.AvailableAmount` = `BudgetAmount - SpentAmount - CommittedAmount`
- `ProjectBudgetVsActual.Variance` = `BudgetAmount - SpentAmount`
- `ProjectBudgetVsActual.UtilizationPercent` = `SpentAmount / BudgetAmount * 100`

## Conventions

- **Read-only:** لا تكتب في DB. أي mutation → ارفع event واترك الـ module المختص يعالجها
- **TenantId mandatory:** كل method يبدأ بـ `tenantId` filter (من `ITenantContext`)
- **CompanyId optional filter:** للـ multi-company isolation
- **DateTime as UTC:** كل الـ dates بـ UTC
- **CancellationToken:** كل الـ async methods تأخذه وتُمرره للـ DB
- **No caching في MVP:** نتركه لـ Redis layer في Phase 3
- **Comments بالعربي** (موجودة في الـ services)

## Test Pattern

يستخدم [`../../Tests/ERPSystem.Tests/Common/FakeDbConnectionFactory`](../../Tests/ERPSystem.Tests/Common/FakeDbConnectionFactory.cs)
لـ in-memory simulation. الـ tests:

- `Tests/Reports/FinanceReportServiceTests.cs` (7 tests)
- `Tests/Reports/InventoryReportServiceTests.cs` (7 tests)
- `Tests/Reports/ProjectReportServiceTests.cs` (6 tests)

**المجموع: 20 unit tests** تغطي كل الـ computed properties + happy path + edge cases.

## Dependencies

- `IDbConnectionFactory` (from `Shared/`)
- `ITenantContext` (from `Shared/MultiTenancy/`)
- Dapper (raw SQL)
- `IProjectRepository`, `IProjectBudgetRepository` (Project module — للـ ProjectReportService فقط)

## Integration مع الباقي

- **Finance:** يقرأ من `accounts`, `journal_lines`, `journal_entries`
- **Inventory:** يقرأ من `stock_levels`, `items`, `warehouses`, `stock_movements`
- **Projects:** يقرأ من `projects`, `project_budgets`, `journal_lines` (joined by `cost_center_id`)

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md) — root
- [`../Finance/AGENTS.md`](../Finance/AGENTS.md)
- [`../Inventory/AGENTS.md`](../Inventory/AGENTS.md)
- [`../Projects/AGENTS.md`](../Projects/AGENTS.md)
- [`../../Shared/AGENTS.md`](../../Shared/AGENTS.md) — IDbConnectionFactory
- [`../../Tests/AGENTS.md`](../../Tests/AGENTS.md) — test patterns
- [`../Procurement/AGENTS.md`](../Procurement/AGENTS.md) — Phase 3 (Vendor Spend report — مستقبلي)
- [`../HR/AGENTS.md`](../HR/AGENTS.md) — Phase 3.5 (Headcount report)
- [`../Payroll/AGENTS.md`](../Payroll/AGENTS.md) — Phase 4 (Payroll summary, EOS liability)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
