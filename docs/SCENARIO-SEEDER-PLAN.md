# 📋 خطة: ScenarioSeeder — بيانات تشغيلية لسنة مالية كاملة

> إنشاء tenant جديد + CoA مخصص + بيانات وهمية متكاملة تمثل سنة تشغيلية.
> السنة المالية: **2026** (يناير — ديسمبر)

---

## 🎯 الهدف

بناء tenant يحاكي شركة ليبية حقيقية (مقاولات + خدمات) — **شركة الأفق للتجارة والمقاولات** — بسنة تشغيلية كاملة:
- 12 شهر رواتب (يناير–ديسمبر 2026)
- حركات مخزون (استلام + صرف)
- مشتريات من موردين (PO → GR → Bill)
- قيود محاسبية (الرواتب المرحّلة + قيود يومية متنوعة)
- حضور وإجازات موظفين
- مشاريع جارية

---

## 🏢 الشركة虚构

**اسم الشركة:** شركة الأفق للتجارة والمقاولات
**القطاع:** مقاولات + خدمات
**العملة:** LYD (دينار ليبي)
**سنة التأسيس:** 2024
**Fiscal Year:** 2026 (يناير–ديسمبر)

---

## 📐 البنية التحية للـ CoA (Chart of Accounts)

نستخدم الـ DefaultCoASeed (47 حساب) كقاعدة + نُضيف حسابات إضافية:

### حسابات إضافية تُنشأ عبر Seeder:

| الكود | الاسم | النوع |
|-------|-------|-------|
| 1500 | أصول ثابتة تحت الإنشاء | Asset |
| 1600 |手里的 (مجمع) | Asset |
| 1700 | ضمانات وم扶着 | Asset |
| 2240 | بنك المدينة | Liability |
| 2250 | صندوق التكافل الاجتماعي | Liability |
| 4120 | مصاريف الكهرباء والمياه | Expense |
| 4130 | مصاريف الاتصالات | Expense |
| 4140 | إيجار المبني | Expense |
| 4150 | مصاريف صيانة vehicles | Expense |
| 4160 | مصاريف تأمين | Expense |
| 4170 | مصاريف قانونية ومحاسبية | Expense |
| 4180 | إهلاك الأصول الثابتة | Expense |
| 4500 | مصاريف أخرى | Expense |
| 5130 | إيرادات خدمات استشارية | Revenue |
| 5140 | إيرادات صيانة | Revenue |
| 6100 | فرق سعر صرف | Other |
| 6200 | خصومات مكتسبة | Other |
| 7100 | مصاريف ح丫لية | Other |

---

## 👥 Departments & Employees (12 موظف)

| ID | الاسم | القسم | الوظيفة | Basic Salary (LYD) |
|----|-------|-------|---------|-------------------|
| 1 | محمد أحمد Franco | الإدارة | مدير عام | 8,500 |
| 2 | أحمد عبدالله | المالية | محاسب أول | 3,800 |
| 3 | Fatima Hamida | المالية | محاسب | 3,200 |
| 4 | خالد محمد | الموارد البشرية | مسؤول HR | 3,500 |
| 5 | سارة علي | الإدارة | أمين سر | 2,800 |
| 6 | عمر يوسف | المشاريع | مهندس موقع | 4,000 |
| 7 | Abdulbasit Salem | المشاريع | فني بناء | 2,200 |
| 8 | Ali Omar | المشاريع | فني بناء | 2,200 |
| 9 | Hussein Mansour | المخازن | أمين مخزن أول | 2,500 |
| 10 | Kamal Ramadan | المخازن | أمين مخزن | 2,200 |
| 11 | نصير علي | المشتريات | مسؤول مشتريات | 3,000 |
| 12 | Rida Khalil | المالية | كاشير | 2,400 |

---

## 💰 Salary Structure لكل موظف

كل موظف عنده:
- **Basic Salary** (الراتب الأساسي)
- **Housing Allowance** = 20% من Basic
- **Transportation Allowance** = 10% من Basic
- **Food Allowance** = 15% من Basic
- **Social Insurance (Employee)** = 3.75% من Basic
- **Libya Tax** = حسب الشريحة التصاعدية

---

## 📅 Attendance (12 شهر × 22 يوم عمل)

| الشهر | أيام العمل | أيام الغياب | ملاحظة |
|-------|-----------|------------|---------|
| يناير | 22 | 1-2 | إجازة春节 (موظف آسيوي) |
| فبراير | 20 | 1 | - |
| مارس | 23 | 0 | - |
| أبريل | 21 | 2 | إجازة Easter |
| مايو | 22 | 1 | - |
| يونيو | 22 | 0 | - |
| يوليو | 23 | 1 | - |
| أغسطس | 22 | 2 | إجازة صيفية |
| سبتمبر | 22 | 1 | - |
| أكتوبر | 23 | 0 | - |
| نوفمبر | 21 | 1 | - |
| ديسمبر | 22 | 3 | أعياد + إجازات |

إجمالي: ~12,672 attendance record (12 موظف × ~22 يوم × 12 شهر)

---

## 🏖️ Leave Requests (إجازات سنوية)

- 8 موظفين أخذوا إجازات (7-14 يوم لكل واحد)
- 3 موظفين عندهم إجازات مرضية (1-3 أيام)
- 2 موظفين عندهم إجازات طارئة

---

## 💸 Payroll Runs (12 شهر)

| الشهر | الحالة | Total Gross | Total Net | ملاحظة |
|-------|--------|-----------|----------|---------|
| Jan 2026 | Posted | ~38,000 | ~33,500 | أول شهر |
| Feb 2026 | Posted | ~38,000 | ~33,500 | - |
| Mar 2026 | Posted | ~38,000 | ~33,500 | - |
| Apr 2026 | Posted | ~38,000 | ~33,500 | - |
| May 2026 | Posted | ~38,000 | ~33,500 | - |
| Jun 2026 | Posted | ~38,000 | ~33,500 | - |
| Jul 2026 | Posted | ~38,000 | ~33,500 | - |
| Aug 2026 | Posted | ~38,000 | ~33,500 | - |
| Sep 2026 | Posted | ~38,000 | ~33,500 | - |
| Oct 2026 | Posted | ~38,000 | ~33,500 | - |
| Nov 2026 | Posted | ~38,000 | ~33,500 | - |
| Dec 2026 | Posted | ~42,000 | ~37,000 | bonus شهر + نهاية السنة |

كل شهر: **Process** → **Post** (يُنشئ Journal Entry تلقائياً)

---

## 🏗️ Vendors (4 موردين)

| الكود | الاسم | النوع | الرصيد المستحق |
|-------|-------|-----|--------------|
| V-001 | مكتب المدينة للبناء | مواد بناء | ~45,000 LYD |
| V-002 | شركة النور للأدوات المكتبية | قرطاسية ومكتبات | ~8,500 LYD |
| V-003 | مؤسسة الوفاء للغذاء | مواد غذائية (إعاشة) | ~12,000 LYD |
| V-004 | شركة النظافة الخضراء | خدمات نظافة | ~6,000 LYD |

---

## 📦 Items (15 صنف)

| الكود | الاسم | التصنيف | التكلفة المتوسطة |
|-------|-------|--------|----------------|
| MAT-001 | إسمنت بورتلاندي | مواد بناء | 28 LYD/شيكالة |
| MAT-002 | حديد تشكيلي 10mm | مواد بناء | 85 LYD/قضيب |
| MAT-003 | رمل نظيف | مواد بناء | 15 LYD/م³ |
| MAT-004 | بلوك أسمنتي 20cm | مواد بناء | 1.5 LYD/قطعة |
| MAT-005 | طوب أحمر | مواد بناء | 0.8 LYD/قطعة |
| OFF-001 | ورق A4 | قرطاسية | 45 LYD/علبة |
| OFF-002 | حبر طابعة HP 304 | قرطاسية | 85 LYD/عبوة |
| OFF-003 | أدوات كتابة متنوعة | قرطاسية | 25 LYD/طقم |
| FOO-001 | مواد غذائية خام | إعاشة | 150 LYD/لتر |
| FOO-002 | مشروبات باردة | إعاشة | 35 LYD/علبة |
| SVC-001 | خدمة نقل | خدمات | 500 LYD/رحلة |
| SVC-002 | خدمة تنظيف | خدمات | 200 LYD/يوم |
| CLE-001 | مواد تنظيف | أدوات نظافة | 60 LYD/لتر |
| EQP-001 | معدات حماية شخصية | أدوات safety | 120 LYD/طقم |
| EQP-002 | قطع غيار vehicles | صيانة vehicles | 350 LYD/قطعة |

---

## 🏭 Warehouses (2 مستودع)

| الكود | الاسم | الموقع |
|-------|-------|-------|
| WH-001 | مستودع المواد الرئيسية | طرابلس - المنطقة الصناعية |
| WH-002 | مستودع القرطاسية والمكتبات | طرابلس - المقر الإداري |

---

## 📋 Purchase Orders (25 PO خلال 2026)

| الربع | عدد POs | إجمالي القيمة |
|-------|--------|-------------|
| Q1 (يناير–مارس) | 7 | ~85,000 LYD |
| Q2 (أبريل–يونيو) | 7 | ~95,000 LYD |
| Q3 (يوليو–سبتمبر) | 6 | ~80,000 LYD |
| Q4 (أكتوبر–ديسمبر) | 5 | ~75,000 LYD |

**الـ status distribution:**
- 22 POs: Received (استلمت كامل)
- 2 POs: Partial (استلم جزء)
- 1 PO: Cancelled

---

## 📥 Goods Receipts (20 GRs)

- 20 GRs مرتبطة بـ 22 POs (كل GR يستلم PO واحد)
- GRs happen 3-7 أيام بعد PO date
- بعض GRs partial (استلام جزئي)

---

## 🧾 Vendor Bills (18 Bills)

- 18 Bill من 20 GRs (90% تُفوتر)
- 2 Bill pending (بانتظار invoice من المورد)
- Payment terms: 30-60 يوم
- December: 3 bills ما زالوا unpaid (تقادم)

---

## 📊 Journal Entries (additional — غير اللي من Payroll)

إضافة قيود يدوية متنوعة:

| التاريخ | الوصف | المبلغ | النوع |
|--------|-------|--------|-------|
| Jan 2026 | إيجار المبني - January | 5,000 | Expense |
| Jan 2026 | كهرباء ومياه | 1,200 | Expense |
| Jan 2026 | فاتورة هاتف | 450 | Expense |
| Feb 2026 | إيجار المبني - February | 5,000 | Expense |
| Feb 2026 | صيانة vehicles | 2,800 | Expense |
| Mar 2026 | تأمين سنوي | 12,000 | Prepaid |
| Apr 2026 | إيجار المبني - April | 5,000 | Expense |
| Jun 2026 | تجديد رخصة Municipality | 3,000 | Expense |
| Aug 2026 | صيانة مكيفات | 1,800 | Expense |
| Oct 2026 | مستلزمات مكتبية كبيرة | 4,500 | Expense |
| Dec 2026 | مكافآت نهاية السنة | 25,000 | Expense |

---

## 🏗️ Projects (3 مشاريع)

| الكود | الاسم | الحالة | القيمة العقد |
|-------|-------|--------|------------|
| P-2026-001 | مشروع بناء فيلات السوurni | جارٍ | 450,000 LYD |
| P-2026-002 | تجديد مبنى municipal | جارٍ | 180,000 LYD |
| P-2026-003 | مشروع صيانة طرق | مُعلق | 95,000 LYD |

---

## 📈 Dashboard KPIs (بعد completion)

الـ Dashboard يجب أن يُظهر بعد الـ seeding:

| المقياس | القيمة المتوقعة |
|---------|---------------|
| إجمالي الموظفين | 12 |
| إجمالي الموردين | 4 |
| طلبات الشراء المفتوحة | 3 |
| الفواتير المستحقة | 3 (تقادم) |
| إجمالي الرواتب المدفوعة (2026) | ~456,000 LYD |
| Journal Entries في الـ GL | ~40+ (12 payroll + ~28 manual) |
| عدد الحسابات النشطة | 47+ |

---

## 🔧 Technical Implementation

### الملفات

```
src/backend/Shared/SeedData/
├── ScenarioSeeder.cs          # IHostedService — orchestrates everything
├── ScenarioData.cs            # Static realistic data (names, amounts, dates)
├── AlFajrCompanySeeding.cs   # Tenant + Holding + CoA extension
└── PayrollSeeder.cs           # 12-month payroll generation
```

### Flag التشغيل

```json
// appsettings.Development.json
{
  "Database": {
    "SeedScenario": true,
    "ScenarioTenantName": "AlFajr Trading & Contracting"
  }
}
```

### Execution Flow

```
1. CreateTenant "AlFajr" → TenantId
2. Register User (admin@alfajr.local / Demo1234)
       ↓
3. Company bootstrap fires (OnTenantCreatedAsync)
   → Holding company "AlFajr Holding"
   → Default CoA (47 accounts)
       ↓
4. ScenarioSeeder (IHostedService) runs on startup:
   a. Add extra CoA accounts (18 new accounts)
   b. Departments (4)
   c. Employees (12) with SalaryStructures
   d. Attendance (12 شهر × 22 يوم)
   e. LeaveRequests
   f. Vendors (4)
   g. Items + Warehouses (2)
   h. Stock Movements (receives + issues)
   i. PurchaseOrders (25) → GoodsReceipts (20) → VendorBills (18)
   j. PayrollRuns (12 شهر: Process + Post)
   k. Manual JournalEntries
   l. Projects (3)
       ↓
5. Mark as seeded (appsettings SeedScenario = false after success)
```

### Referential Integrity Rules

- Attendance → Employee (CASCADE)
- LeaveRequest → Employee
- SalaryStructure → Employee
- PayrollRun → processed via PayrollService (uses SalaryStructures)
- PayrollItem → Employee + PayrollRun
- PurchaseOrder → Vendor
- GoodsReceipt → PurchaseOrder + Warehouse
- VendorBill → GoodsReceipt
- JournalEntry → Account (per line)
- StockMovement → Item + Warehouse + (optional) Project

### Idempotency

الـ Seeder يتحقق من وجود البيانات قبل الإنشاء:
```csharp
if (await _employees.AnyAsync(e => e.TenantId == tenantId)) return; // already seeded
```

---

## ⏱️ حجم البيانات

| Entity | العدد |
|--------|-------|
| Departments | 4 |
| Employees | 12 |
| SalaryStructures | 12 |
| Attendance Records | ~12,672 |
| LeaveRequests | ~15 |
| Vendors | 4 |
| Items | 15 |
| Warehouses | 2 |
| StockMovements | ~200 |
| StockLevels | ~30 |
| PurchaseOrders | 25 |
| GoodsReceipts | 20 |
| VendorBills | 18 |
| PayrollRuns | 12 |
| PayrollItems | ~144 (12 × 12) |
| PayslipComponents | ~720 (144 × 5) |
| JournalEntries | ~40 |
| JournalLines | ~120 |
| Projects | 3 |
| **Total** | **~13,500 record** |

---

## ✅ Acceptance Criteria

بعد completion، يجب:
- [ ] Login بـ `admin@alfajr.local / Demo1234` → Dashboard يُظهر 12 employee, 4 vendors
- [ ] `GET /api/hr/payroll/runs` → 12 run (Jan–Dec 2026) كلهم Posted
- [ ] `GET /api/finance/ledger/trial-balance` → أرصدة على الحسابات (رواتب، مصروفات، الخ)
- [ ] `GET /api/procurement/vendors` → 4 vendors
- [ ] `GET /api/procurement/purchase-orders` → 25 POs
- [ ] `GET /api/inventory/levels/low-stock` → some items (ناقص reorder level)
- [ ] `GET /api/projects` → 3 projects
- [ ] Frontend: الـ Dashboard widgets تُظهر أرقام حقيقية

---

## 📝 التسمية Libya-Specific

- Company: **شركة الأفق للتجارة والمقاولات** (Al-Fajr Trading & Contracting)
- Currency: **LYD** (Libyan Dinar)
- Arabic RTL everywhere
- Libyan-style names (Arabic + some foreign workers)
- Fiscal year: Gregorian (يناير–ديسمبر)
- Address format: **طرابلس، ليبيا**

---

## 📄 التسلسل الزمني (Timeline)

| الشهر | الرواتب | المشتريات | المخزون | Journal Entries |
|-------|---------|----------|---------|----------------|
| يناير | ✅ | PO#1-3 | GR#1-2 | JE#1-4 |
| فبراير | ✅ | PO#4-5 | GR#3-4 | JE#5-7 |
| مارس | ✅ | PO#6-7 | GR#5-6 | JE#8-10 |
| أبريل | ✅ | PO#8 | GR#7 | JE#11-12 |
| مايو | ✅ | PO#9-10 | GR#8-9 | JE#13-15 |
| يونيو | ✅ | PO#11-12 | GR#10-11 | JE#16-18 |
| يوليو | ✅ | PO#13 | GR#12 | JE#19-21 |
| أغسطس | ✅ | PO#14-15 | GR#13-14 | JE#22-24 |
| سبتمبر | ✅ | PO#16-17 | GR#15-16 | JE#25-27 |
| أكتوبر | ✅ | PO#18-19 | GR#17 | JE#28-30 |
| نوفمبر | ✅ | PO#20-21 | GR#18-19 | JE#31-33 |
| ديسمبر | ✅ | PO#22-25 | GR#20 | JE#34-40 |

---

## 📋 ملخص Plan

**الهدف:** إنشاء tenant جديد "AlFajr" + بيانات تشغيلية لسنة 2026 كاملة
**الـ scope:** Backend seeder + API verification + frontend verification
**الـ output:** tenant يعمل بالكامل يُستخدم لـ demos و testing
**الـ timeline:** ~200-300 lines C# code + 1 hour execution

