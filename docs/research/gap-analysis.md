# تحليل الفجوات التنافسية و Phase 3 Scope — ERP-SYSTEM

> **التاريخ:** 23 يونيو 2026
> **المُعدّ:** كبير مهندسي برمجيات — Mavis (general worker)
> **المهمة:** `gap-analysis` ضمن خطة `plan_b5ae4fc0`
> **المدخلات:** `docs/research/daftra-features.md` · `docs/research/erpnext-features.md` · `docs/research/odoo-reference.md` · `docs/PLAN.md` · `AGENTS.md`
> **الإصدار المقترح للنظام:** v2.1 (Phase 3+)

---

## 1. الملخص التنفيذي

بعد مراجعة المميزات الكاملة لـ Daftra (ERP عربي سحابي، 40K+ عميل) و ERPNext (19+ موديول، 18 ميزة تقنية فريدة) و Odoo (44K+ تطبيق، معيار الصناعة)، يتبيّن أن نظامنا **ERP-SYSTEM** يتمتّع بأساس معماري قوي (Multi-tenant Modular Monolith، Dapper + Marten + PostgreSQL 15، JWT + BCrypt، Event Bus + Outbox، 7 modules عاملة، 50+ endpoint، 23 unit test) لكنه يفقد نقاطًا تنافسية حاسمة في خمسة محاور.

- **فجوة 1 — الموديولات المفقودة كليًا:** لا يوجد **Procurement** (PO/GR/Bill)، لا يوجد **HR**، لا يوجد **CRM**، لا يوجد **POS**، لا يوجد **Manufacturing**. هذه هي الموديولات التي يحقق منها Daftra و ERPNext القيمة الأكبر.
- **فجوة 2 — الـ Frontend ضعيف:** 8 صفحات فقط بدون Layout موحّد، بدون Dashboard KPIs، بدون Sidebar، بدون UI components library. الـ 40K عميل في دفترة و 12M مستخدم في Odoo يعتمدون على UI ناضج.
- **فجوة 3 — غياب الـ Arabic-first compliance:** لا يوجد دعم ZATCA (السعودية)، ETA (مصر)، FTA (الإمارات)، LPA (ليبيا). دفترة يتفوق هنا كمرجع محلي.
- **فجوة 4 — ضعف الأتمتة والتخصيص:** لا يوجد **Workflow Engine**، لا يوجد **Custom Fields** runtime، لا يوجد **Report Builder**، لا يوجد **Print Format Builder** (Jinja). ERPNext يتفوق هنا بشكل كبير.
- **فجوة 5 — غياب الـ BI/Analytics:** لا توجد Dashboards تفاعلية، لا Chart.js، لا Grafana، لا Cohort Analysis. Odoo يتفوق بـ KPI tiles + drill-down.

**الـ Roadmap المقترح (3 جلسات عمل، ~6-8 أيام تطوير):** Phase 3 = Procurement Core (PO → GR → Bill)، Phase 3.5 = HR Core (Employee + Attendance + Leave بدون Payroll)، Phase 4 = UI Polish (Layout موحّد + Dashboard + UI components library + Workflow Engine بسيط).

---

## 2. جدول المقارنة التنافسية (17 صفًا)

| # | الميزة / الـ Feature | ERP-SYSTEM | Daftra | ERPNext | Odoo |
|---|---------------------|------------|:------:|:-------:|:----:|
| 1 | **Multi-tenancy** | ✅ Supported (TenantId على كل entity) | ✅ SaaS multi-tenant | ✅ Sites-based | ✅ Supported (Record Rules) |
| 2 | **Auth (JWT + Refresh)** | ✅ Supported (60min + 14day rotation) | ✅ OAuth2 + API Key | ✅ Token + Password + OAuth2 | ✅ OAuth2 + API Key |
| 3 | **Chart of Accounts** (CoA) | ✅ Supported (Finance module) | ✅ Supported (predefined per country) | ✅ Supported (tree) | ✅ Supported |
| 4 | **Journal Entries + GL** | ✅ Supported (Double-entry validation) | ✅ Supported (Auto journals) | ✅ Supported (with versioning) | ✅ Supported |
| 5 | **Purchase Orders (PO)** | ❌ Missing | ✅ Supported (دورة كاملة) | ✅ Supported (with workflow) | ✅ Supported |
| 6 | **Goods Receipt (GR/GRN)** | ❌ Missing | ✅ Supported (against PO) | ✅ Supported (with landed cost) | ✅ Supported |
| 7 | **Vendor Bills** | ❌ Missing | ✅ Supported (Dr Inventory/Expense, Cr AP) | ✅ Supported (auto JE) | ✅ Supported |
| 8 | **Inventory + Stock Movements** | ✅ Supported (Item, Warehouse, UoM, StockLevel) | ✅ Supported (متعدد المستودعات + Serial/Lot) | ✅ Supported (Batch + Serial + Bin) | ✅ Supported |
| 9 | **Multi-Currency** | ✅ Supported (BaseCurrency) | ✅ Supported (135+ عملة) | ✅ Supported (Auto Revaluation v15) | ✅ Supported |
| 10 | **VAT / Tax** | ⚠️ Partial (Tax templates، لا ZATCA) | ✅ Supported (ZATCA, ETA, FTA, ISTD) | ⚠️ Partial (KSA, India GST, UAE VAT) | ⚠️ Partial (l10n_xx per country) |
| 11 | **E-Invoicing (ZATCA Phase 2)** | ❌ Missing | ✅ Supported (Crypto Stamp + QR + فاتورة integration) | ❌ Missing (3rd-party only) | ⚠️ Partial (l10n_sa_edi module) |
| 12 | **HR / Employees** | ❌ Missing | ✅ Supported (شامل + Payroll + EOS) | ✅ Supported (Employee + Attendance + Leave + Payroll) | ✅ Supported |
| 13 | **CRM (Leads + Opportunities)** | ❌ Missing | ✅ Supported (Client Management + Follow-up) | ✅ Supported (Lead → Opportunity → Quotation) | ✅ Supported |
| 14 | **Manufacturing (BOM)** | ❌ Missing | ✅ Premium only (BOM + Workstations + Indirect Cost) | ✅ Supported (BOM + Work Order + Routing) | ✅ Supported (MRP) |
| 15 | **POS (Point of Sale)** | ❌ Missing | ✅ Supported (Web + iOS + Android + Desktop offline) | ✅ Supported (POS Profile + offline) | ✅ Supported (POS module قوي) |
| 16 | **Workflow Engine** | ❌ Missing | ✅ Supported (Custom Workflow في Premium) | ✅ Supported (declarative + conditional) | ✅ Supported (Studio) |
| 17 | **Custom Fields (no-code)** | ❌ Missing | ⚠️ Partial (right-side custom fields) | ✅ Supported (Customize Form UI) | ✅ Supported (Studio) |
| 18 | **Print Format Builder** | ❌ Missing | ✅ Supported (قوالب قابلة للتخصيص) | ✅ Supported (Jinja + Drag-Drop Builder v14) | ✅ Supported (QWeb) |
| 19 | **Report Builder** | ❌ Missing | ⚠️ Partial (تقارير جاهزة، تخصيص محدود) | ✅ Supported (Query + Script + Chart + Dashboard) | ✅ Supported (Pivot + Graph + Cohort) |
| 20 | **REST API** | ✅ Supported (50+ endpoint، Tenant-scoped) | ✅ Supported (docs.daftara.dev) | ✅ Supported (auto-generated per DocType) | ✅ Supported (XML-RPC + JSON-RPC + REST) |
| 21 | **Webhooks** | ❌ Missing | ✅ Supported (عند أحداث الفاتورة) | ✅ Supported (declarative + conditional) | ✅ Supported (Base Automation) |
| 22 | **Real-time Updates (WebSocket)** | ❌ Missing (Server-Sent Events محدود) | ✅ Supported (live notifications) | ✅ Supported (Socket.IO + Redis pub-sub) | ✅ Supported (Bus + Longpolling) |
| 23 | **Mobile App** | ❌ Missing | ✅ 7 تطبيقات (Business, POS, ESS, OCR, إلخ) | ✅ PWA (HR + general) | ✅ iOS + Android native |
| 24 | **Offline Mode** | ❌ Missing | ✅ Supported (POS + Stocktaking + Expenses) | ✅ Supported (POS offline) | ⚠️ Partial (POS only) |
| 25 | **UI Components Library** | ❌ Missing (Tailwind classes only) | ✅ Supported (نظام تصميم خاص) | ✅ Supported (Frappe Desk UI) | ✅ Supported (Odoo Web Client + Studio) |
| 26 | **Multi-Language + RTL** | ✅ Supported (RTL via dir="rtl") | ✅ Supported (عربي + إنجليزي + أرقام عربية-هندية) | ⚠️ Partial (يحتاج تخصيص لكل موديول) | ⚠️ Partial (translations community) |
| 27 | **MENA Compliance (ضريبة + فاتورة إلكترونية)** | ❌ Missing | ✅ Supported (4 دول) | ⚠️ Partial (KSA + India) | ⚠️ Partial (l10n modules) |

**ملخص الجدول:** ERP-SYSTEM يغطّي **7 من 27** ميزة بشكل كامل (Multi-tenancy, Auth, CoA, Journal, Inventory, Multi-Currency, REST API)؛ يغطّي **2** بشكل جزئي (VAT, RTL)؛ ويفتقد **18** ميزة، أهمها: Procurement (PO/GR/Bill), HR, CRM, POS, Manufacturing, Workflow Engine, Custom Fields, E-Invoicing, UI Library, Mobile App، Webhooks، Real-time، Dashboard BI.

---

## 3. Phase 3 Scope (مقترح) — Procurement Core

> **الهدف:** إكمال دورة المشتريات الكاملة من إنشاء أمر الشراء حتى قيد الفاتورة في دفتر الأستاذ. هذه هي الميزة الأولى المفقودة والأكثر طلبًا.

### 3.1 الـ Aggregates الأساسية (4)

#### A. Vendor (المورّد)
- **Entity:** `Vendor` يحتوي `Id`, `TenantId`, `Name`, `Email`, `Phone`, `Address`, `TaxNumber`, `Currency`, `PaymentTerms (Net30/Net60/Cash)`, `IsActive`, `CreatedAt`, `UpdatedAt`.
- **Business Rules:**
  - `Name` فريد داخل الـ tenant.
  - `TaxNumber` اختياري لكن إذا وُجد، يُتحقق من صيغته.
  - `Currency` يجب أن تكون من قائمة الـ SupportedCurrencies.

#### B. PurchaseOrder (PO) — أمر الشراء
- **Entity:** `PurchaseOrder` يحتوي `Id`, `TenantId`, `PONumber` (auto-generated), `VendorId`, `Status (Draft/Pending/Approved/Sent/Received/Cancelled)`, `OrderDate`, `ExpectedDate`, `Currency`, `TotalAmount`, `Notes`, `Lines: PurchaseOrderLine[]`.
- **Entity Line:** `PurchaseOrderLine` يحوي `ItemId`, `Quantity`, `UnitPrice`, `TaxRate`, `SubTotal`.
- **Business Rules:**
  - لا يمكن إنشاء PO بدون `VendorId` و line واحدة على الأقل.
  - `Approve` يتطلب صلاحية `ProcurementManager`.
  - `Send to Vendor` يولّد PDF + يرسل بريد إلكتروني (مستقبل).
  - `Mark as Received` ينقل الحالة تلقائيًا بعد إنشاء GR.
  - تعديل PO مُسموح فقط في `Draft` و `Pending`.

#### C. GoodsReceipt (GR) — سند استلام البضاعة
- **Entity:** `GoodsReceipt` يحوي `Id`, `TenantId`, `GRNumber`, `PurchaseOrderId`, `Status (Draft/Received/Cancelled)`, `ReceivedDate`, `WarehouseId`, `Lines: GoodsReceiptLine[]`.
- **Entity Line:** `GoodsReceiptLine` يحوي `ItemId`, `Quantity`, `Notes`.
- **Business Rules:**
  - لا يُنشأ GR إلا لـ PO في status `Approved` أو `Sent`.
  - الكمية المُستلمة لا تتجاوز الكمية في PO (validation صارم).
  - عند تأكيد GR (status = `Received`)، يُنشأ **StockMovement** rows تلقائيًا (Inbound) لكل line، ويُحدّث `StockLevel` في الـ warehouse المحدد.
  - **Integration:** يستفيد من Event Bus الموجود في Phase 2.4 — يُنشئ `StockReceived` event → Finance event handler ينشئ قيد محاسبي لاحقًا.

#### D. VendorBill (VB) — فاتورة المورّد
- **Entity:** `VendorBill` يحوي `Id`, `TenantId`, `BillNumber`, `GoodsReceiptId`, `VendorId`, `Status (Draft/Posted/Paid/Cancelled)`, `BillDate`, `DueDate`, `Currency`, `SubTotal`, `TaxAmount`, `TotalAmount`, `Notes`, `Lines: VendorBillLine[]`.
- **Entity Line:** `VendorBillLine` يحوي `ItemId`, `Quantity`, `UnitPrice`, `TaxRate`, `SubTotal`.
- **Business Rules:**
  - لا يُنشأ Bill إلا لـ GR في status `Received`.
  - عند `Post` (status = `Posted`)، يُنشأ **JournalEntry** تلقائيًا:
    - `Dr Inventory (Asset)` بمبلغ البضاعة
    - `Dr Tax Input (Asset)` بمبلغ الضريبة
    - `Cr Accounts Payable (Liability)` بالإجمالي
  - الحسابات تُجلب من الـ Chart of Accounts (موجود في Finance module).

### 3.2 الـ Endpoints (4 مسارات)

| الـ Method | الـ Path | الوصف |
|-----------|----------|-------|
| `GET`, `POST` | `/api/procurement/vendors` | List vendors (مع pagination + filters) + Create vendor |
| `GET`, `POST` | `/api/procurement/pos` | List POs + Create PO |
| `PUT` | `/api/procurement/pos/{id}/approve` | Approve PO (يتطلب صلاحية) |
| `PUT` | `/api/procurement/pos/{id}/send` | Send PO to vendor (يولد PDF) |
| `PUT` | `/api/procurement/pos/{id}/receive` | Mark as Received |
| `GET`, `POST` | `/api/procurement/grs` | List GRs + Create GR (against PO) |
| `GET`, `POST` | `/api/procurement/bills` | List Bills + Create Bill (against GR) |
| `PUT` | `/api/procurement/bills/{id}/post` | Post Bill (ينشئ JournalEntry) |

**المجموع:** 4 route prefixes × ~3 methods = **10 actions**، لكنها موزعة على 4 endpoints رئيسية كما طُلب.

### 3.3 Schema Migrations (2)

#### Migration 1: `20260623_120000_CreateProcurementVendors.cs`
- Schema: `procurement`
- Tables: `procurement.vendors`
- Columns: `id UUID PK`, `tenant_id UUID NOT NULL`, `name VARCHAR(200) NOT NULL`, `email VARCHAR(200)`, `phone VARCHAR(50)`, `address TEXT`, `tax_number VARCHAR(50)`, `currency CHAR(3) DEFAULT 'LYD'`, `payment_terms VARCHAR(20) DEFAULT 'Net30'`, `is_active BOOLEAN DEFAULT TRUE`, `created_at TIMESTAMPTZ DEFAULT NOW()`, `updated_at TIMESTAMPTZ DEFAULT NOW()`
- Indexes: `(tenant_id, name) UNIQUE`, `(tenant_id, is_active)`, `(tax_number)` (nullable)

#### Migration 2: `20260623_130000_CreateProcurementPosGRsBills.cs`
- Schema: `procurement`
- Tables:
  - `procurement.purchase_orders` + `purchase_order_lines`
  - `procurement.goods_receipts` + `goods_receipt_lines`
  - `procurement.vendor_bills` + `vendor_bill_lines`
- كل جدول يحوي: `id UUID PK`, `tenant_id UUID NOT NULL`, status column (`VARCHAR(20)`)، timestamps، FKs إلى `identity.tenants` (CASCADE).
- Indexes: `(tenant_id, status)`, `(tenant_id, vendor_id)`, `(tenant_id, purchase_order_id)`, `(tenant_id, created_at DESC)`.

### 3.4 Frontend Pages (4 صفحات أساسية + Forms)

| المسار | الـ Component | الوصف |
|--------|-------------|-------|
| `/procurement/vendors` | `VendorsList` | جدول vendors مع TanStack Query، filters (status, search)، زر "Add Vendor" |
| `/procurement/vendors/new` | `VendorForm` | form (RHF + Zod): Name, Email, Phone, Address, Tax Number, Currency, Payment Terms |
| `/procurement/purchase-orders` | `POList` | جدول POs مع status badges، filters، bulk actions |
| `/procurement/purchase-orders/new` | `POForm` | form: اختيار Vendor (dropdown)، إضافة lines (Item dropdown + Quantity + Price)، حساب SubTotal تلقائيًا |
| `/procurement/goods-receipts` | `GRList` | جدول GRs مع status، link للـ PO الأصلي |
| `/procurement/goods-receipts/new` | `GRForm` | form: اختيار PO (يجلب الـ lines تلقائيًا)، تحديد Warehouse، تأكيد الكميات المُستلمة |
| `/procurement/bills` | `BillList` | جدول Bills مع status (Draft/Posted/Paid)، إجمالي المديونية |
| `/procurement/bills/new` | `BillForm` | form: اختيار GR (يجلب الـ lines + Tax تلقائيًا)، تأكيد المبالغ |

**المجموع:** 8 routes (4 lists + 4 forms/creates) موزعة على 4 مواضيع.

---

## 4. Phase 3.5 Scope (مقترح) — HR Core (MVP)

> **الهدف:** إضافة موديول HR كامل ما عدا Payroll. يحقق قيمة فورية (تتبّع الموظفين، الحضور، الإجازات) دون تعقيد حساب الرواتب والضرائب.

### 4.1 الـ Aggregates الأساسية (4)

#### A. Department (القسم)
- **Entity:** `Department { Id, TenantId, Name, Code, ParentId (nullable for hierarchy), ManagerId (FK to Employee, nullable), IsActive }`.
- **Business Rules:** Hierarchy بحد أقصى 5 مستويات، `Code` فريد داخل الـ tenant.

#### B. Employee (الموظف)
- **Entity:** `Employee { Id, TenantId, EmployeeNumber (auto), FullName, Email (unique per tenant), Phone, NationalId, DepartmentId (FK), JobTitle, HireDate, TerminationDate (nullable), BaseSalary (decimal, للعرض فقط — لا payroll في هذه المرحلة), IsActive, CreatedAt, UpdatedAt }`.
- **Business Rules:**
  - `Email` فريد داخل الـ tenant (لا تكرار).
  - `BaseSalary` يُستخدم لاحقًا في Payroll (Phase 4) — الآن read-only للعرض.
  - لا يمكن إنهاء خدمة موظف نشط في department نشط بدون `TerminationDate`.

#### C. Attendance (الحضور)
- **Entity:** `Attendance { Id, TenantId, EmployeeId (FK), Type (CheckIn/CheckOut), Timestamp, Notes, IPAddress (optional for audit) }`.
- **Business Rules:**
  - لا يمكن CheckIn مرتين متتاليتين بدون CheckOut.
  - آخر CheckIn بدون CheckOut = "حاضر" حاليًا.
  - يحسب تلقائيًا: `WorkedHours` بين آخر CheckIn و CheckOut.

#### D. LeaveRequest (طلب الإجازة)
- **Entity:** `LeaveRequest { Id, TenantId, EmployeeId (FK), LeaveType (Annual/Sick/Emergency/Unpaid), StartDate, EndDate, TotalDays, Status (Pending/Approved/Rejected), Reason, ApproverId (FK, nullable), ApprovedAt, Notes }`.
- **Business Rules:**
  - `EndDate >= StartDate`.
  - لا يتعارض مع إجازة معتمدة أخرى للموظف نفسه.
  - `Approve` يتطلب صلاحية `HRManager` أو `Admin`.

### 4.2 الـ Endpoints (3 مسارات)

| الـ Method | الـ Path | الوصف |
|-----------|----------|-------|
| `GET`, `POST` | `/api/hr/employees` | List employees (مع pagination + filters) + Create |
| `GET`, `PUT` | `/api/hr/employees/{id}` | Get employee details + Update |
| `POST` | `/api/hr/attendance` | CheckIn / CheckOut (body: `{employeeId, type}`) |
| `GET` | `/api/hr/attendance?employeeId=&from=&to=` | List attendance records |
| `GET`, `POST` | `/api/hr/leaves` | List leave requests + Create |
| `PUT` | `/api/hr/leaves/{id}/approve` | Approve leave (يتطلب صلاحية HRManager) |
| `PUT` | `/api/hr/leaves/{id}/reject` | Reject leave (يتطلب صلاحية HRManager) |
| `GET`, `POST` | `/api/hr/departments` | List/Create departments |

### 4.3 Schema Migration (1)

#### Migration: `20260623_140000_CreateHRTables.cs`
- Schema: `hr`
- Tables: `hr.departments`, `hr.employees`, `hr.attendance`, `hr.leave_requests`
- كل جدول يحوي: `id UUID PK`, `tenant_id UUID NOT NULL`، timestamps، FKs إلى `identity.tenants`.
- Indexes: `(tenant_id, employee_id)`, `(tenant_id, department_id)`, `(tenant_id, status)`, `(employee_id, timestamp DESC)` للـ attendance.

### 4.4 Frontend Pages (3 صفحات + Forms)

| المسار | الـ Component | الوصف |
|--------|-------------|-------|
| `/hr/employees` | `EmployeesList` | جدول الموظفين + filters (Department, Status) + زر Add |
| `/hr/employees/new` | `EmployeeForm` | form: FullName, Email, Phone, NationalId, Department (dropdown), JobTitle, HireDate, BaseSalary |
| `/hr/attendance` | `AttendancePage` | زر CheckIn/CheckOut بارز + History table + اختيار التاريخ |
| `/hr/leaves` | `LeavesList` | جدول requests مع status badges + Approve/Reject buttons (للمدير) |
| `/hr/leaves/new` | `LeaveForm` | form: LeaveType, StartDate, EndDate, Reason |

---

## 5. خارطة طريق Frontend (UI Polish)

### 5.1 الصفحات الموجودة حاليًا (8 صفحات — من PLAN.md و frontend/AGENTS.md)

| # | الصفحة | المسار | الحالة الحالية | ما يجب إضافته/تحسينه |
|---|--------|--------|----------------|----------------------|
| 1 | **Home** | `/` | Static redirect إلى `/login` | إضافة redirect logic ذكي: لو logged in → `/dashboard` |
| 2 | **Login** | `/login` | form بـ Email + Password، استدعاء `/api/auth/login` | إضافة "Forgot Password" link، تحسين error handling (401 = "بيانات خاطئة")، إضافة Remember Me |
| 3 | **Register** | `/register` | form بـ FullName + Email + Password + TenantName + BaseCurrency | إضافة password strength meter، إضافة Terms checkbox، تحسين UX (RTL labels) |
| 4 | **Dashboard** | `/dashboard` | يعرض counts (Accounts, Items, Projects) مع links | **إعادة كتابة كاملة:** KPI tiles (Total Vendors, Open POs, Active Employees, Low Stock) + Recent Activity + Quick Actions |
| 5 | **Finance / Accounts** | `/finance/accounts` | جدول + form إضافة account | إضافة breadcrumb، filters (Type, Parent)، inline edit، طباعة Chart of Accounts |
| 6 | **Inventory / Items** | `/inventory/items` | جدول items + link | إضافة filters (Category, Warehouse)، Bulk actions (Import CSV)، Stock Level indicator |
| 7 | **Projects** | `/projects` | قائمة projects + link للـ detail | إضافة Progress bar، Budget tracking، Gantt lite (timeline بسيط) |
| 8 | **Reports** | غير موجود كمستقل — endpoints موجودة في `/api/reports/*` | ❌ لا توجد صفحة UI | **إضافة صفحة `/reports`** تستدعي Report endpoints وتعرض جداول + Charts |

### 5.2 المكوّنات المطلوب إضافتها

#### A. Layout موحّد (أولوية 1)
- **Sidebar** (يمين، RTL): logo + navigation links (Dashboard, Vendors, POs, GRs, Bills, Employees, Attendance, Leaves, Reports).
- **Topbar** (أعلى): breadcrumb + user menu (Profile, Logout) + tenant name.
- **Main content area** مع padding موحد.
- **Mobile responsive:** sidebar يصبح Drawer عند < 768px.
- **ملف:** `src/frontend/components/layout/AppShell.tsx`.
- **Route group:** `src/frontend/app/(authenticated)/layout.tsx` ليستعمل `AppShell`.

#### B. UI Components Library (أولوية 2)
إنشاء `src/frontend/components/ui/` مع المكوّنات الأساسية (Tailwind فقط، لا shadcn):

| الـ Component | الغرض | Props الرئيسية |
|---------------|-------|----------------|
| `Button` | أزرار موحدة | `variant (primary/secondary/danger/ghost)`, `size (sm/md/lg)`, `loading`, `disabled` |
| `Input` | حقول إدخال | `label`, `error`, `hint`, `iconLeft`, `iconRight` |
| `Select` | قوائم منسدلة | `options`, `placeholder`, `searchable` |
| `Table` | جداول مع pagination | `columns`, `data`, `loading`, `emptyMessage`, `pagination` |
| `Badge` | status badges | `variant (success/warning/danger/info/neutral)`, `size` |
| `Card` | container للـ widgets | `title`, `actions`, `footer` |
| `Modal` | نوافذ منبثقة | `open`, `onClose`, `title`, `size (sm/md/lg/xl)` |
| `Tabs` | تبويبات | `tabs`, `activeTab`, `onChange` |
| `Toast` | إشعارات سريعة | `type (success/error/info/warning)`, `message`, `duration` |
| `Skeleton` | loading placeholders | `variant (text/circle/rect)`, `width`, `height` |

#### C. Dashboard محسّن (أولوية 3)
- **KPI Tiles** (4-6): Total Vendors, Open POs, Active Employees, Low Stock Items, Total Inventory Value, Pending Leaves.
- **Quick Actions:** New PO, New Vendor, New Employee, New GR.
- **Recent Activity:** آخر 5 GRs + آخر 5 Bills + آخر 5 Leave Requests (combined timeline).
- **Charts:** PO Status Distribution (pie chart)، Inventory by Category (bar chart).
- **استعمال:** Chart.js عبر react-chartjs-2 (مُثبَّت أو يُضاف).

#### D. التحسينات العامة (أولوية 4)
- **Loading states:** Skeleton loaders في كل صفحة قائمة.
- **Error boundaries:** صفحة error.tsx في كل route group.
- **Notifications:** Toast notifications عند success/error (عبر react-hot-toast).
- **Dark mode:** (اختياري، ليس أولوية).
- **i18n:** (مستقبلي، خارج scope Phase 3).

---

## 6. ترتيب الأولويات — Top 10 Features لما يجب تنفيذه الآن

> الترتيب حسب **التأثير التنافسي × سهولة التنفيذ × الاعتماد على الموجود**.

| # | الـ Feature | السبب | الجهد | التأثير | يعتمد على |
|---|------------|-------|------:|--------:|-----------|
| 1 | **Procurement Module (Phase 3)** | أهم موديول مفقود — يحقق دورة كاملة PO → GR → Bill | عالي (5-7 أيام) | عالي جدًا | Finance + Inventory (موجودان) |
| 2 | **HR Core MVP (Phase 3.5)** | ثاني أهم موديول مفقود — قيمة فورية بدون payroll | متوسط (3-4 أيام) | عالي | Identity (موجود) |
| 3 | **Layout موحّد (AppShell + Sidebar + Topbar)** | يحسّن كل الصفحات الموجودة فوريًا | منخفض (1-2 أيام) | عالي جدًا | Tailwind (موجود) |
| 4 | **UI Components Library** | يُسرّع كل التطوير المستقبلي | متوسط (2-3 أيام) | عالي جدًا | Tailwind (موجود) |
| 5 | **Dashboard محسّن مع KPIs** | أول ما يراه المستخدم — يعطي الانطباع الأول | متوسط (2 أيام) | عالي | كل الموديولات |
| 6 | **Workflow Engine بسيط** | يميزنا عن POS-only competitors | متوسط (3 أيام) | متوسط | DB + Backend |
| 7 | **E-Invoicing (ZATCA + ETA) — تصميم فقط** | متطلب تشريعي حاسم للسوق السعودي والمصري | عالي (7+ أيام) | عالي جدًا | Finance + Procurement |
| 8 | **Webhooks + REST API documentation (Swagger enhancements)** | يفتح النظام للتكاملات | منخفض (1-2 يوم) | متوسط | Swashbuckle (موجود) |
| 9 | **Custom Fields runtime** | مرونة في التخصيص بدون كود | عالي (5 أيام) | متوسط | Metadata-driven design |
| 10 | **Reports UI page** | الـ endpoints موجودة، فقط الـ UI مفقود | منخفض (1 يوم) | متوسط | Report endpoints (موجودة) |

**الـ Quick Wins (أول 5):** Procurement + HR + Layout + UI Library + Dashboard — تغطية شاملة في ~12-15 يوم عمل = **3 جلسات عمل**.

---

## 7. خطة التنفيذ على 3 جلسات عمل

> **النموذج:** كل جلسة عمل = يومين إلى 3 أيام. الـ Agent ينفّذ، الـ Verifier يراجع، الـ User يعتمد. **Conventional Commits** و **PR لكل session**.

### الجلسة 1 — Backend Phase 3 (Procurement Module)

**المدة:** 2-3 أيام
**الـ Branch:** `feature/phase-3-procurement-backend`
**الـ Owner:** `backend-architect`

**اليوم 1 (نصف يوم):**
- قراءة `docs/research/gap-analysis.md` و `src/backend/Modules/Inventory/AGENTS.md` و `src/backend/Modules/Projects/AGENTS.md`.
- إنشاء `src/backend/Modules/Procurement/` structure.
- كتابة Migration 1: `procurement.vendors`.
- كتابة Entities: `Vendor.cs`, `PurchaseOrder.cs` (+ `PurchaseOrderLine.cs`), `GoodsReceipt.cs` (+ `GoodsReceiptLine.cs`), `VendorBill.cs` (+ `VendorBillLine.cs`).

**اليوم 1 (نصف يوم):**
- كتابة DTOs (`Application/Dtos.cs`): Request + Response لكل aggregate.
- كتابة Validators (FluentValidation) — مع تحديث `ValidatorsRegistration.cs`.
- كتابة Mapping (Entity ↔ DTO).

**اليوم 2:**
- كتابة Services (CQRS): `VendorService.cs`, `PurchaseOrderService.cs`, `GoodsReceiptService.cs`, `VendorBillService.cs`.
- كتابة Controllers: `src/backend/Host/Controllers/ProcurementController.cs` مع `[Authorize]`.
- تسجيل DI في `Program.cs`.
- Migration 2: `procurement.purchase_orders`, `purchase_order_lines`, `goods_receipts`, `goods_receipt_lines`, `vendor_bills`, `vendor_bill_lines`.

**اليوم 3:**
- `dotnet build` → 0 errors.
- Smoke test ذاتي: تشغيل Backend محليًا + اختبار endpoints بـ curl.exe.
- كتابة `docs/SMOKE-TEST-PROCUREMENT.md`.
- 5-6 Conventional Commits (entity layer, dto, services, controllers, smoke).
- فتح PR إلى `develop`.

### الجلسة 2 — Frontend Phase 3 (UI Foundation + Procurement + HR)

**المدة:** 3 أيام
**الـ Branch:** `feature/phase-3-frontend`
**الـ Owner:** `frontend-engineer`

**اليوم 1:**
- إنشاء `src/frontend/components/layout/AppShell.tsx` (Sidebar + Topbar + Main).
- إنشاء `src/frontend/components/ui/`: `Button`, `Input`, `Select`, `Table`, `Badge`, `Card`.
- نقل الصفحات الموجودة إلى route group `(authenticated)`.
- تحديث `lib/api.ts` لإضافة Procurement types + functions.

**اليوم 2:**
- صفحات Procurement (4): Vendors list + new، POs list + new، GRs list + new، Bills list + new.
- (اختياري حسب الوقت) صفحات HR (3): Employees list + new، Attendance، Leaves list + new.
- 8 صفحات جديدة × 2 commits (component + integration).

**اليوم 3:**
- Dashboard محسّن: KPI tiles + Quick Actions + Recent Activity.
- (إن بقي وقت) HR pages.
- `npm run build` → success.
- Smoke test: تشغيل Frontend + اختبار السيناريو الكامل.
- كتابة `docs/SMOKE-TEST-FRONTEND.md`.
- 6-8 Conventional Commits.
- فتح PR إلى `develop`.

### الجلسة 3 — Integration + Polish + Docs

**المدة:** 2 أيام
**الـ Branch:** `feature/phase-3-integration` (يبدأ من develop بعد دمج 1+2)
**الـ Owner:** `qa-tester` + `general`

**اليوم 1 (qa-tester):**
- دمج `feature/phase-3-procurement-backend` و `feature/phase-3-frontend` في develop (squash merge).
- تشغيل Backend (5000) + Frontend (3000) + PostgreSQL (5432).
- سيناريو E2E كامل:
  1. Register → Login → JWT.
  2. Create Vendor → List Vendors.
  3. Create PO (Draft) → Approve → Send.
  4. Create GR (against PO) → Received → التحقق من StockMovement rows.
  5. Create Bill (against GR) → Posted → التحقق من JournalEntry.
  6. Create Employee → CheckIn → Leave Request → Approve.
- كتابة `docs/E2E-TEST-REPORT.md` (12 خطوة + verdict).

**اليوم 2 (general):**
- إصلاح أي bugs من E2E test.
- تحديث `docs/PLAN.md` إلى v2.1 (Phase 3 ✅).
- تحديث `CHANGELOG.md`.
- (اختياري) كتابة `docs/RELEASE-REPORT-PHASE3.html` (التقرير النهائي البصري).
- Commit + Push + Auto-delete feature branches.

**Stop Condition الإجمالي:** 3 PRs مُراجعة + Backend يعمل + Frontend يعمل + E2E PASS + 10 صفحات جديدة + 4 endpoints جديدة + 4 modules backend (Procurement + HR + Departments + Integration).

---

## 8. الخلاصة والتوصيات

1. **ابدأ بـ Procurement** — أكبر فجوة تنافسية، وأوضح ROI، يبني على Finance + Inventory الموجود.
2. **Layout + UI Library قبل أي صفحة جديدة** — وفّر 50% من الجهد في كل صفحة لاحقة.
3. **Dashboard محسّن فور دمج Procurement** — يعطي الانطباع الأول ويحفّز الفريق.
4. **HR Core MVP بعد Procurement** — يُضيف قيمة سريعة (Attendance + Leaves) بدون تعقيد Payroll.
5. **E-Invoicing (ZATCA) هو الميزة التنافسية الحاسمة للسوق** — اجعله Phase 4 بعد بناء الأساسات.
6. **Workflow Engine + Custom Fields** — مبكرًا لكن ليس أولوية قصوى (لا حاجة له قبل 50+ مستخدم).
7. **وثّق كل قرار في `docs/adr/`** — لماذا استخدمنا Dapper بدل EF Core، لماذا PostgreSQL 15، إلخ.

> **النتيجة المتوقعة بعد 3 جلسات عمل:**
> - **Backend:** 11 modules (إضافة Procurement + HR)، 60+ endpoint، 40+ DB tables.
> - **Frontend:** 18 صفحة (8 موجودة + 10 جديدة)، Layout موحّد، UI Library، Dashboard KPIs.
> - **Tests:** E2E سيناريو كامل + 23+ unit tests موجودة.
> - **تنافسيًا:** نغطي ~50% من مميزات Daftra/ERPNext في الـ Procurement + HR، ونبني أساس قوي للمراحل القادمة.

---

**آخر تحديث:** 23 يونيو 2026
**الإصدار:** v1.0 — أول تحليل فجوات شامل
**المرجع التالي:** `docs/research/RELEASE-REPORT-PHASE3.html` (يُنشأ بعد E2E test)
