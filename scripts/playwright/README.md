# ERP-SYSTEM E2E Tests (Playwright)

## Quick Start

```powershell
# 1. تأكد إن ERP-SYSTEM شغّال على Docker
cd F:\erpsystem7-23-2026\ERP-SYSTEM
.\scripts\INSTALL-ULTIMATE.ps1

# 2. Install Playwright
cd scripts\playwright
npm install
npx playwright install --with-deps chromium

# 3. شغّل الاختبارات
npx playwright test
# أو بـ UI:
npx playwright test --headed
```

## المدة

- **~5-8 دقائق** للـ 60+ test
- يختبر كل صفحة × 4 عمليات (List + Get + Create + Edit)
- يكشف أي **500 errors** تلقائياً عبر network listener

## المخرجات

```
PASS  Auth > Login page loads
PASS  Auth > Login with valid credentials
PASS  AR: Customers > List page loads without 500 or 403
PASS  AR: Customers > Create new customer (this was failing with 500)
...

Total: 60 passed, 0 failed, 5 skipped
Time: 5m 23s
```

## الـ coverage

| Module | List | Create | Edit | Delete |
|--------|:----:|:------:|:----:|:------:|
| Auth | ✓ | - | - | - |
| Finance: Accounts | ✓ | ✓ | ✓ | - |
| Finance: Cost Centers | ✓ | ✓ | - | - |
| Finance: Journal Entries | ✓ | ✓ | - | - |
| AR: Customers | ✓ | ✓ | ✓ | - |
| AR: Sales Invoices | ✓ | ✓ | - | - |
| AR: Receipts | ✓ | ✓ | - | - |
| Procurement: Vendors | ✓ | ✓ | - | - |
| Procurement: POs | ✓ | ✓ | - | - |
| Procurement: GRs | ✓ | ✓ | - | - |
| Procurement: Bills | ✓ | ✓ | - | - |
| Inventory: Items | ✓ | ✓ | - | - |
| Inventory: Categories | ✓ | ✓ | - | - |
| Inventory: Warehouses | ✓ | ✓ | - | - |
| Inventory: UoM | ✓ | ✓ | - | - |
| Inventory: Stock Levels | ✓ | - | - | - |
| Inventory: Movements | ✓ | ✓ | - | - |
| Inventory: Reservations | ✓ | - | - | - |
| HR: Employees | ✓ | ✓ | - | - |
| HR: Departments | ✓ | ✓ | - | - |
| HR: Attendance | ✓ | - | - | - |
| HR: Leaves | ✓ | ✓ | - | - |
| HR: Payroll | ✓ | ✓ | - | - |
| HR: Salary Structures | ✓ | ✓ | - | - |
| Payments | ✓ | ✓ | - | - |
| Projects | ✓ | ✓ | - | - |
| Admin: Companies | ✓ | ✓ | - | - |
| Admin: Posting Rules | ✓ | ✓ | - | - |
| Admin: Notifications | ✓ | - | - | - |
| Reports (4) | ✓ | - | - | - |
| Dashboard | ✓ | - | - | - |

**Total: 30+ page types × 4 ops = 60+ tests**

## كيف يكتشف الـ 500 errors

كل test بيستمع لـ network responses. لو أي request رجع 500، الـ test يفشل تلقائياً مع الـ URL.

```typescript
page.on('response', (response) => {
  if (response.status() === 500) {
    errors.push(`${response.status()} ${response.url()}`);
  }
});
```

## CI/CD Integration

```yaml
# GitHub Actions example
- name: Run E2E tests
  run: |
    cd scripts/playwright
    npm install
    npx playwright install --with-deps chromium
    npx playwright test
- uses: actions/upload-artifact@v3
  with:
    name: playwright-report
    path: scripts/playwright/playwright-report/
```
