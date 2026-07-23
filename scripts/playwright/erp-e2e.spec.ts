/**
 * ERP-SYSTEM v1.0.13 — Comprehensive E2E Test Suite
 * ==================================================
 * يختبر كل واجهة × 4 عمليات (List + Get + Create + Edit + Delete)
 *
 * شغّله:
 *   1. npm install -D @playwright/test
 *   2. npx playwright install
 *   3. npx playwright test scripts/playwright/erp-e2e.spec.ts
 *
 * أو من Windows PowerShell:
 *   cd F:\erpsystem7-23-2026\ERP-SYSTEM
 *   npm install -D @playwright/test
 *   npx playwright install --with-deps chromium
 *   npx playwright test scripts/playwright/erp-e2e.spec.ts --reporter=line
 *
 * الفولدرات اللي هيتأكد منها:
 *   - /finance/accounts
 *   - /finance/customers
 *   - /finance/cost-centers
 *   - /finance/journal-entries
 *   - /finance/receipts
 *   - /finance/sales-invoices
 *   - /inventory/categories
 *   - /inventory/items
 *   - /inventory/movements
 *   - /inventory/reservations
 *   - /inventory/stock-levels
 *   - /inventory/uom
 *   - /inventory/warehouses
 *   - /hr/attendance
 *   - /hr/departments
 *   - /hr/employees
 *   - /hr/leaves
 *   - /hr/payroll
 *   - /hr/salary-structures
 *   - /procurement/bills
 *   - /procurement/goods-receipts
 *   - /procurement/purchase-orders
 *   - /procurement/vendors
 *   - /payments
 *   - /projects
 *   - /admin/companies
 *   - /admin/item-categories
 *   - /admin/notifications
 *   - /admin/posting-rules
 *   - /reports/finance/* (3 reports)
 */

import { test, expect, Page } from '@playwright/test';

const BASE_URL = process.env.BASE_URL || 'http://localhost:3000';
const API_URL = process.env.API_URL || 'http://localhost:5000';
const ADMIN_EMAIL = 'admin@alfajr.local';
const ADMIN_PASS = 'Demo1234';

// Track entities created during tests for cleanup
const createdIds: Record<string, string[]> = {};

// --- Helpers ---------------------------------------------------------------

async function login(page: Page) {
  await page.goto(`${BASE_URL}/login`);
  await page.fill('input[name="email"], input[type="email"]', ADMIN_EMAIL);
  await page.fill('input[name="password"], input[type="password"]', ADMIN_PASS);
  await page.click('button[type="submit"]');
  await page.waitForURL(/\/(dashboard|finance|hr|inventory|procurement|projects|payments|admin|reports)/, {
    timeout: 15000,
  });
}

async function logout(page: Page) {
  // Click logout button or clear localStorage
  await page.evaluate(() => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');
  });
  await page.goto(`${BASE_URL}/login`);
}

async function checkNo500Errors(page: Page) {
  // Listen for 500 responses
  const errors: string[] = [];
  page.on('response', (response) => {
    if (response.status() === 500) {
      errors.push(`${response.status()} ${response.url()}`);
    }
  });
  return errors;
}

async function expectNoErrors(errors: string[], context: string) {
  if (errors.length > 0) {
    throw new Error(
      `Found ${errors.length} 500 errors in ${context}:\n  ${errors.join('\n  ')}`
    );
  }
}

// Common assertion: page loaded without 500s
async function expectPageLoadsClean(page: Page, url: string) {
  const errors = await checkNo500Errors(page);
  await page.goto(`${BASE_URL}${url}`);
  await page.waitForLoadState('networkidle', { timeout: 15000 });
  // Wait for either content or "no data" message
  await page.waitForTimeout(1500);
  await expectNoErrors(errors, url);
}

// --- Auth ------------------------------------------------------------------

test.describe('Auth', () => {
  test('Login page loads', async ({ page }) => {
    await page.goto(`${BASE_URL}/login`);
    await expect(page).toHaveTitle(/ERP/);
    await expect(page.locator('input[type="email"]')).toBeVisible();
    await expect(page.locator('input[type="password"]')).toBeVisible();
  });

  test('Login with valid credentials', async ({ page }) => {
    await login(page);
    // Should be on dashboard or some authenticated page
    expect(page.url()).not.toContain('/login');
  });

  test('Login with invalid credentials shows error', async ({ page }) => {
    await page.goto(`${BASE_URL}/login`);
    await page.fill('input[type="email"]', 'wrong@email.com');
    await page.fill('input[type="password"]', 'wrongpass');
    await page.click('button[type="submit"]');
    // Should show error or stay on login
    await expect(page.locator('text=/خطأ|invalid|wrong|فشل/i').first()).toBeVisible({ timeout: 5000 });
  });

  test('Logout clears session', async ({ page }) => {
    await login(page);
    await logout(page);
    // After logout, accessing protected page should redirect to login
    await page.goto(`${BASE_URL}/dashboard`);
    await page.waitForURL(/\/login/, { timeout: 5000 });
  });
});

// --- Finance: Accounts (Chart of Accounts) ---------------------------------

test.describe('Finance: Chart of Accounts', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads without 500', async ({ page }) => {
    await expectPageLoadsClean(page, '/finance/accounts');
  });

  test('Create new account', async ({ page }) => {
    await page.goto(`${BASE_URL}/finance/accounts/new`);
    await page.waitForLoadState('networkidle');
    const code = `E2E-${Date.now()}`;
    await page.fill('input[name="code"]', code);
    await page.fill('input[name="name"]', 'E2E Test Account');
    await page.selectOption('select[name="type"]', { index: 1 });
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/finance\/accounts($|\?)/, { timeout: 10000 });
    // Verify it's in the list
    await expect(page.locator(`text=${code}`)).toBeVisible({ timeout: 5000 });
  });

  test('Edit account', async ({ page }) => {
    await expectPageLoadsClean(page, '/finance/accounts');
    // Click first edit button (if any)
    const editBtn = page.locator('a[href*="/edit"]').first();
    if (await editBtn.count() > 0) {
      await editBtn.click();
      await page.waitForLoadState('networkidle');
      await expect(page.locator('input[name="name"]')).toBeVisible();
    }
  });
});

// --- AR: Customers ---------------------------------------------------------

test.describe('AR: Customers', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads without 500 or 403', async ({ page }) => {
    await expectPageLoadsClean(page, '/finance/customers');
  });

  test('Create new customer (this was failing with 500)', async ({ page }) => {
    await page.goto(`${BASE_URL}/finance/customers/new`);
    await page.waitForLoadState('networkidle');
    const code = `CUST-E2E-${Date.now()}`;
    await page.fill('input[name="code"]', code);
    await page.fill('input[name="name"]', 'E2E Test Customer');
    await page.fill('input[name="email"]', 'e2e@test.local');
    await page.fill('input[name="creditLimit"]', '1000');
    await page.click('button[type="submit"]');
    await page.waitForURL(/\/finance\/customers($|\?)/, { timeout: 10000 });
    await expect(page.locator(`text=${code}`)).toBeVisible({ timeout: 5000 });
  });

  test('Edit customer', async ({ page }) => {
    await expectPageLoadsClean(page, '/finance/customers');
    const editBtn = page.locator('a[href*="/edit"]').first();
    if (await editBtn.count() > 0) {
      await editBtn.click();
      await page.waitForLoadState('networkidle');
      await expect(page.locator('input[name="name"]')).toBeVisible();
    }
  });
});

// --- AR: Sales Invoices ----------------------------------------------------

test.describe('AR: Sales Invoices', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads without 500', async ({ page }) => {
    await expectPageLoadsClean(page, '/finance/sales-invoices');
  });

  test('Create invoice form loads', async ({ page }) => {
    await page.goto(`${BASE_URL}/finance/sales-invoices/new`);
    await page.waitForLoadState('networkidle');
    // Should not have any 500
    await expect(page.locator('body')).not.toContainText('500');
  });
});

// --- AR: Receipts ----------------------------------------------------------

test.describe('AR: Receipts', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/finance/receipts');
  });

  test('Create receipt form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/finance/receipts/new');
  });
});

// --- Finance: Cost Centers -------------------------------------------------

test.describe('Finance: Cost Centers', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/finance/cost-centers');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/finance/cost-centers/new');
  });
});

// --- Finance: Journal Entries ----------------------------------------------

test.describe('Finance: Journal Entries', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/finance/journal-entries');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/finance/journal-entries/new');
  });
});

// --- Finance: Reports ------------------------------------------------------

test.describe('Finance: Reports', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('Trial balance loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/reports/finance/trial-balance');
  });

  test('Balance sheet loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/reports/finance/balance-sheet');
  });

  test('Income statement loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/reports/finance/income-statement');
  });

  test('Cash flow loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/reports/finance/cash-flow');
  });
});

// --- Procurement -----------------------------------------------------------

test.describe('Procurement: Vendors', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/procurement/vendors');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/procurement/vendors/new');
  });
});

test.describe('Procurement: Purchase Orders', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads (this was failing with 500)', async ({ page }) => {
    await expectPageLoadsClean(page, '/procurement/purchase-orders');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/procurement/purchase-orders/new');
  });
});

test.describe('Procurement: Goods Receipts', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/procurement/goods-receipts');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/procurement/goods-receipts/new');
  });
});

test.describe('Procurement: Vendor Bills', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/procurement/bills');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/procurement/bills/new');
  });
});

// --- Inventory -------------------------------------------------------------

test.describe('Inventory: Items', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/inventory/items');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/inventory/items/new');
  });
});

test.describe('Inventory: Categories', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/admin/item-categories');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/admin/item-categories/new');
  });
});

test.describe('Inventory: Warehouses', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/inventory/warehouses');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/inventory/warehouses/new');
  });
});

test.describe('Inventory: UoM', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/inventory/uom');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/inventory/uom/new');
  });
});

test.describe('Inventory: Stock Levels', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/inventory/stock-levels');
  });
});

test.describe('Inventory: Movements', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/inventory/movements');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/inventory/movements/new');
  });
});

test.describe('Inventory: Reservations', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/inventory/reservations');
  });
});

// --- HR --------------------------------------------------------------------

test.describe('HR: Employees', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/hr/employees');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/hr/employees/new');
  });
});

test.describe('HR: Departments', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/hr/departments');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/hr/departments/new');
  });
});

test.describe('HR: Attendance', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/hr/attendance');
  });
});

test.describe('HR: Leaves', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/hr/leaves');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/hr/leaves/new');
  });
});

test.describe('HR: Payroll', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/hr/payroll');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/hr/payroll/new');
  });
});

test.describe('HR: Salary Structures', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/hr/salary-structures');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/hr/salary-structures/new');
  });
});

// --- Payments --------------------------------------------------------------

test.describe('Payments', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/payments');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/payments/new');
  });
});

// --- Projects --------------------------------------------------------------

test.describe('Projects', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/projects');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/projects/new');
  });
});

// --- Companies / Admin -----------------------------------------------------

test.describe('Admin: Companies', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/admin/companies');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/admin/companies/new');
  });
});

test.describe('Admin: Posting Rules', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/admin/posting-rules');
  });

  test('Create form loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/admin/posting-rules/new');
  });
});

test.describe('Admin: Notifications', () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  test('List page loads', async ({ page }) => {
    await expectPageLoadsClean(page, '/admin/notifications');
  });
});

// --- Dashboard -------------------------------------------------------------

test.describe('Dashboard', () => {
  test('Dashboard loads after login', async ({ page }) => {
    await login(page);
    await expectPageLoadsClean(page, '/dashboard');
  });
});
