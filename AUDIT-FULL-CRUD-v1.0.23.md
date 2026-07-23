# v1.0.23 Audit Report — Full CRUD Review

## ملخص تنفيذي

مراجعة شاملة لجميع صفحات CRUD في الـ frontend (65 صفحة) وربطها مع الـ backend (25 controller, 100+ endpoint). الـ audit كشف عن 4 فئات من المشاكل:

| الفئة | عدد المشاكل | الأثر |
|-------|-------------|------|
| **A. Edit/Delete مفقود في الـ lists** | 18 list page | مستخدم لا يستطيع تعديل/حذف أغلب الكيانات |
| **B. Combobox/Input خاطئ** | 14 form | مستخدم يكتب UUIDs يدوياً، خطأ مضمون |
| **C. Edit لا يحفظ** | 2-3 forms | PUT 200 لكن لا يُحدِّث الـ state |
| **D. UX: combobox label / receipt sign** | 4 forms | عرض نص غريب، calculation خاطئ |

---

## الفئة A: Lists بدون Edit/Delete (18 page)

### A1. Lists بـ Edit (Pencil) لكن بدون Delete
- `finance/accounts/page.tsx`
- `finance/cost-centers/page.tsx`
- `admin/item-categories/page.tsx`
- `hr/departments/page.tsx`
- `hr/salary-structures/page.tsx`
- `inventory/items/page.tsx`
- `inventory/warehouses/page.tsx`
- `projects/page.tsx`

**الإصلاح:** إضافة Trash2 icon بجانب Pencil.

### A2. Lists بدون Edit ولا Delete (تحتاج كلاهما)
- `finance/customers/page.tsx`
- `finance/receipts/page.tsx`
- `finance/sales-invoices/page.tsx`
- `finance/journal-entries/page.tsx`
- `hr/employees/page.tsx`
- `hr/leaves/page.tsx`
- `hr/payroll/page.tsx`
- `inventory/movements/page.tsx`
- `inventory/reservations/page.tsx`
- `inventory/uom/page.tsx`
- `procurement/bills/page.tsx`
- `procurement/goods-receipts/page.tsx`
- `procurement/purchase-orders/page.tsx`
- `payments/page.tsx`

**الإصلاح:** إضافة Pencil + Trash2 لكل row، مع links لـ `/edit` و modal/confirm للـ delete.

### A3. Lists معقدة (special handling)
- `admin/companies/page.tsx` (tree view - edit modal)
- `admin/notifications/page.tsx` (read-only عادة)
- `admin/posting-rules/page.tsx` (technical)
- `hr/attendance/page.tsx` (date-based)
- `inventory/stock-levels/page.tsx` (read-only)
- `finance/aging-ar/page.tsx` (report - read-only)
- `reports/*` (read-only)

---

## الفئة B: Forms مع Input حر بدل Combobox (14 form)

| الـ Form | الحقل الحالي | المصدر المطلوب | الـ API |
|---------|--------------|----------------|--------|
| `finance/accounts/new` | "الحساب الأب" (UUID input) | قائمة الحسابات | `GET /api/finance/accounts?includeInactive=true` |
| `finance/accounts/[id]/edit` | "الحساب الأب" (UUID input) | قائمة الحسابات | نفسه |
| `finance/cost-centers/new` | "المركز الأب (Parent ID)" | قائمة CostCenters | `GET /api/cost-centers` |
| `finance/customers/new` | لا Company dropdown (single-company) | قائمة Companies | `GET /api/companies` |
| `hr/departments/new` | "القسم الأب" + "مدير القسم" | departments + users | `GET /api/hr/departments` + users |
| `hr/departments/[id]/edit` | نفس | نفسه | نفسه |
| `hr/employees/new` | لا Department dropdown | departments | `GET /api/hr/departments` |
| `hr/employees/[id]/edit` | نفس | نفسه | نفسه |
| `inventory/items/new` | "وحدة القياس" (input) | UoM list | `GET /api/inventory/uom` |
| `inventory/items/[id]/edit` | نفس | نفسه | نفسه |
| `inventory/movements/new` | "الصنف" + "المستودع" + "الشركة" | items + warehouses + companies | 3 endpoints |
| `inventory/reservations/new` | "الصنف" + "المستودع" + "معرّف المرجع" | items + warehouses | 2 endpoints |
| `inventory/warehouses/new` | "مدير المخزن (User ID)" | users list | users endpoint |
| `inventory/warehouses/[id]/edit` | نفس | نفسه | نفسه |
| `procurement/purchase-orders/new` | "المورّد" (Combobox ✓ لكن lines.itemId input) | vendors + items | 2 endpoints |
| `procurement/bills/new` | نفس | نفسه | نفسه |
| `procurement/goods-receipts/new` | نفس | نفسه | نفسه |
| `finance/sales-invoices/new` | العميل ✓ Combobox لكن format label خاطئ + lines.itemId input | customers + items | 2 endpoints |

**الإصلاح:** تحويل كل Input مع placeholder="UUID" إلى Select مع options loaded من الـ API.

---

## الفئة C: Edit لا يحفظ (مشكلة accounts)

**الأعراض:**
- المستخدم يضغط pencil → edit form يفتح
- يغير Type من Asset إلى Liability
- يحفظ
- PUT يرجع 200
- لكن الـ list ما يحدّث، أو يحدّث لكن الحقول ما تنعكس

**الـ root cause المحتمل:**
- `financeApi.updateAccount` يبعت `Partial<Account>` بما فيه `code: undefined` (و Account.code غير موجود في UpdateAccountRequest DTO)
- الـ backend يحدّث لكن الـ state في الـ list يبقى `accounts` cached
- بعد router.push → list، لكن cache list غير invalid

**الإصلاح:**
1. تأكد `financeApi.updateAccount` يبعت الحقول الصحيحة فقط
2. تأكد `AccountsController.Update` يبطل cache
3. تأكد الـ list page ينادي `load()` بعد navigate

---

## الفئة D: UX Issues

### D1. Receipts combobox "العميل" يعرض نص مقطوع "—001 ش—"
- الـ `customerOptions` يبني label: `${c.code} — ${c.name}`
- الـ Select native قد يقطع text طويل
- **الإصلاح:** truncate بـ CSS `text-overflow: ellipsis` أو shorten label إلى `${c.code} ${c.name.slice(0, 20)}`

### D2. Receipts "التخصيص على الفواتير" يعرض نص طويل "22, تصنيف 22 — SI-2026-000002 — 23/07/2026"
- الـ `invoiceOptions` يبني label طويل جداً
- الـ "22" في البداية يبدو أنه line number من index list
- **الإصلاح:** shorten label إلى `SI-2026-000002 | متبقي: 100.00 LYD`

### D3. Receipt sign convention
- الـ user قال "يظهر بالسالب"
- في الـ UI: "مجموع التخصيصات" قد يعرض بالسالب لو الـ user كتب رقم سالب في حقل "المبلغ"
- لكن الـ backend يقبل أرقام موجبة فقط (validation `Math.Abs(totalAllocated - req.Amount) < 0.0001`)
- **الإصلاح:** تأكد input min=0 + step=positive، أضف placeholder "0.00"، اجعل validation تظهر بالأحمر

### D4. Accounts normal balance "دائن" لـ Asset
- الـ accounts (1110, 1120) نوعهم Asset لكن الرصيد "دائن"
- الـ service يحسب NormalBalance من Type (Asset → Debit)
- لكن في الـ DB، الـ seeded data قد يكون NormalBalance=0 (default enum)
- **الإصلاح:** عند seed، تأكد NormalBalance=1 للـ Asset/Expense، NormalBalance=2 للـ Liability/Equity/Revenue. أو أضف migration لتحديث البيانات الموجودة.

---

## خطة التنفيذ (v1.0.23)

### Step 1: Lists (يوم 1)
- إضافة Trash2 لكل list page في A1
- إضافة Pencil + Trash2 لكل list page في A2
- استخدام helper `useEntityActions` لتسهيل الـ maintenance

### Step 2: Comboboxes (يوم 2)
- تحويل كل Input مع placeholder UUID إلى Select dropdown
- تحميل options من API عند mount
- تطبيق pattern: `useState + useEffect` لfetch

### Step 3: Edit fixes (يوم 3)
- debug accounts edit: تحقق من cache invalidation
- debug receipts edit: تحقق من form fields
- debug items edit: تحقق من field mapping

### Step 4: UX polish (يوم 4)
- truncate long labels في Select
- validate positive amounts
- seed correct NormalBalance
- fix receipt sign convention

### Step 5: Build & test
- backend build: 0 errors
- frontend build: 65/65 pages
- npm test: 419 passing

---

## الأرقام الإجمالية

| البند | العدد |
|-------|------|
| Total list pages | 18 |
| Lists with Edit (need Delete) | 8 |
| Lists without Edit/Delete | 14 |
| Forms with free-text UUID | 14 |
| Forms with broken Edit | 2-3 |
| Total lines to change (approx) | ~600 |
| Estimated time | 4-6 hours |
