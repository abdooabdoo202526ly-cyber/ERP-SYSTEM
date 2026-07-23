# 🎨 src/frontend/AGENTS.md

> Next.js 14 Frontend — Phase 4 (مكتمل: AppShell + 8 UI components + 24 صفحة عبر Phase 2.5+ / 3 / 3.5 / 4).
>
> محدّث: 2026-06-24

## شو فيه (حالياً)

```
frontend/
├── app/
│   ├── page.tsx                                  # الصفحة الرئيسية
│   ├── layout.tsx                                # Root layout (RTL, dir="rtl")
│   ├── globals.css                               # Tailwind directives + globals
│   ├── login/page.tsx                            # POST /api/auth/login
│   ├── register/page.tsx                         # POST /api/auth/register
│   └── (authenticated)/                          # Route group — صفحات محمية بـ AppShell
│       ├── layout.tsx                            # يستعمل AppShell (sidebar + topbar + breadcrumb)
│       ├── dashboard/page.tsx                    # KPIs + quick actions
│       ├── finance/accounts/page.tsx             # GET/POST /api/finance/accounts
│       ├── inventory/items/page.tsx              # GET /api/inventory/items
│       ├── projects/page.tsx                     # GET /api/projects
│       ├── procurement/                          # Phase 3 (Procurement)
│       │   ├── vendors/page.tsx + vendors/new/page.tsx
│       │   ├── purchase-orders/page.tsx + purchase-orders/new/page.tsx
│       │   ├── goods-receipts/page.tsx + goods-receipts/new/page.tsx
│       │   └── bills/page.tsx + bills/new/page.tsx
│       ├── hr/                                   # Phase 3.5 (HR Core) + Phase 4 (Payroll)
│       │   ├── employees/page.tsx + employees/new/page.tsx
│       │   ├── attendance/page.tsx               # CheckIn/CheckOut + history
│       │   ├── leaves/page.tsx + leaves/new/page.tsx
│       │   └── payroll/                          # Phase 4 (Payroll + EOS)
│       │       ├── page.tsx                      # List of PayrollRuns
│       │       ├── new/page.tsx                  # Create new PayrollRun
│       │       └── [id]/
│       │           ├── page.tsx                  # PayrollRun detail + items
│       │           └── payslip/[empId]/page.tsx  # Employee payslip view
├── components/                                   # Phase 3
│   ├── layout/
│   │   ├── AppShell.tsx                          # Sidebar + Topbar + Breadcrumb + User menu
│   │   └── Sidebar.tsx                           # Navigation menu (Phase 3 + Phase 4 Payroll entry)
│   └── ui/                                       # 8 مكونات مكتوبة بـ Tailwind (لا shadcn)
│       ├── Button.tsx
│       ├── Input.tsx
│       ├── Select.tsx
│       ├── Table.tsx
│       ├── Badge.tsx
│       ├── Card.tsx
│       ├── Modal.tsx
│       ├── PageHeader.tsx
│       └── index.ts                              # barrel export
├── lib/
│   ├── api.ts                                    # Axios + JWT interceptors + 9 API namespaces
│   ├── useAuth.ts                                # Hook للمصادقة (user + token state)
│   └── utils.ts                                  # Helpers (formatCurrency, formatDate, cn, ...)
├── package.json
├── next.config.js
├── tailwind.config.js
└── tsconfig.json
```

## Phase Status

| Phase | المحتوى | الحالة |
|-------|---------|--------|
| Phase 2.5+ | 8 صفحات أولية + Auth + Dashboard | ✅ مكتمل |
| **Phase 3** | **AppShell + 8 UI components + 8 صفحات Procurement** | **✅ مكتمل** |
| **Phase 3.5** | **HR Core pages (employees + attendance + leaves)** | **✅ مكتمل** |
| **Phase 4** | **Payroll pages (list + new + detail + payslip) + hrApi.payroll.*** | **✅ مكتمل** |
| Phase 5 | Inventory UI v2 + Manufacturing pages | 📋 قادم |

**مجموع الصفحات الحالي: 24 صفحة** (8 Phase 2.5+ + 8 Procurement + 4 HR + 4 Payroll)

## Tech Stack (الفعلية في package.json)

| الحزمة | الإصدار | الغرض |
|------|---------|-------|
| **next** | 14.2.0 | Framework |
| **react** + **react-dom** | 18.3 | UI runtime |
| **typescript** | 5.5+ | Strict mode |
| **tailwindcss** | 3.4 | **UI الوحيد المُطبَّق** |
| **axios** | 1.7+ | HTTP client + interceptors |
| **@tanstack/react-query** | 5.0+ | Server state (مُثبَّت) |
| **react-hook-form** | 7.52+ | Forms (مُثبَّت) |
| **zod** | 3.23+ | Runtime validation (مُثبَّت) |
| **@hookform/resolvers** | 3.7+ | zod ↔ react-hook-form |
| **date-fns** | 3.6+ | Date formatting |
| **lucide-react** | 0.400+ | Icons |
| **shadcn-ui** | 0.8.0 | ⚠️ **CLI generator فقط** (لم يُستخدم لإضافة components) |

> **ملاحظة مهمة:** AGENTS السابقة ذكرت "shadcn/ui" كـ UI components. هذا غير دقيق في يونيو 2026:
> - `shadcn-ui` package موجودة في devDependencies كـ **CLI** (لتوليد components).
> - لم يُشغَّل `shadcn-ui init` ولا `shadcn-ui add`، فمجلد `components/ui/` **غير موجود**.
> - كل الـ UI حالياً مكتوب بـ **Tailwind classes مباشرة** (انظر `app/register/page.tsx`).

## Conventions

- **Functional components** فقط
- **Hooks** للمنطق المشترك (لا HOCs)
- **Client Components** فقط عند الحاجة (`'use client'`)، الباقي Server Components
- **API calls** عبر `lib/api.ts` (Axios + interceptors)
- **JWT storage**: `localStorage` (مؤقت في dev) — `accessToken`, `refreshToken`, `user`
- **Axios interceptor** يجدد Access Token تلقائياً على 401
- **Comments بالعربي**، identifiers بالإنجليزي
- **RTL**: كل الـ pages تحوي `dir="rtl"` على `<main>` أو `<html>`

## Auth Integration (الفعلية)

### Auth API Contracts (مطابقة لـ `lib/api.ts` و `AuthDtos.cs`)

```typescript
// POST /api/auth/register
interface RegisterRequest {
  email: string;            // required, email format
  password: string;         // required, ≥8 chars, [A-Z], [a-z], [0-9]
  fullName: string;         // required, ≤200 chars
  tenantName: string;       // مطلوب لإنشاء tenant جديد (يُولّد Subdomain تلقائياً)
  baseCurrency?: string;    // optional, default "LYD"
  // ❌ لا يوجد حقل "subdomain" — يُحسب تلقائياً عبر Slugify(TenantName)
}

// POST /api/auth/login
interface LoginRequest {
  email: string;
  password: string;
  tenantId?: string;        // optional (Guid) — إن لم يُرسل، بحث شامل
  // ❌ لا يوجد "tenantSubdomain" — الـ backend لا يستقبله
}

// AuthResponse (مشترك بين register و login)
interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;  // ISO datetime
  refreshTokenExpiresAt: string; // ISO datetime
  user: UserInfo;
  holdingCompanyId: string;      // Guid — للـ multi-company bootstrap
}

interface UserInfo {
  id: string;             // Guid
  tenantId: string;       // Guid
  email: string;
  fullName: string;
  roles: string[];        // ["Admin", "Accountant", "ProjectManager", "Viewer"]
}
```

### مسارات الـ Auth

| المسار | الـ Method | يطلب Auth | الوصف |
|------|------------|----------|-------|
| `/api/auth/register` | POST | لا | ينشئ tenant + user، أو يضيف user لـ tenant موجود |
| `/api/auth/login` | POST | لا | يرجع tokens |
| `/api/auth/refresh` | POST | لا | يجدد tokens (Token rotation) |
| `/api/auth/logout` | POST | نعم | يلغي Refresh Token |
| `/api/auth/me` | GET | نعم | يرجع UserInfo من JWT claims |

### Flow (صفحة `/register`)

1. المستخدم يدخل: fullName, email, password, tenantName
2. `authApi.register({...})` → `POST /api/auth/register`
3. الـ backend يحسب `Subdomain = Slugify(tenantName)` تلقائياً
4. ينشئ `Tenant` + `User` + 4 default roles + يربط User بدور `Admin`
5. يخزّن `accessToken` + `refreshToken` + `user` في `localStorage`
6. `router.push('/dashboard')`

### Flow (صفحة `/login`)

1. المستخدم يدخل: email, password
2. `authApi.login({email, password})` → `POST /api/auth/login`
3. يخزّن tokens في `localStorage`
4. `router.push('/dashboard')`

## API Namespaces في `lib/api.ts`

| Namespace | Methods | الـ Module |
|-----------|---------|-----------|
| `authApi` | register, login, refresh, logout, me | Identity |
| `financeApi` | accounts, journal, ledger, postingRules | Finance |
| `projectsApi` | projects, tasks, resources | Projects |
| `inventoryApi` | items, categories, warehouses, stock | Inventory |
| `reportsApi` | various reports | Reports |
| `companiesApi` | companies, costCenters | Companies |
| `procurementApi` | vendors, purchaseOrders, goodsReceipts, bills | Procurement (Phase 3) |
| `hrApi` | departments, employees, attendance, leaves | HR (Phase 3.5) |
| **`hrApi.payroll`** | **`listRuns, getRun, createRun, processRun, postRun, getItems, getPayslip, getEosPreview`** | **Payroll (Phase 4)** 🆕 |

## Phase 4 — Payroll Pages (NEW)

### 1. `app/(authenticated)/hr/payroll/page.tsx` — List
- جدول بكل الـ PayrollRuns (period + status + totals)
- Status badges: Draft (gray) / Processed (blue) / Posted (green) / Voided (red)
- زر "إنشاء Run جديد" → `/hr/payroll/new`
- Pagination (50 per page)

### 2. `app/(authenticated)/hr/payroll/new/page.tsx` — Create
- نموذج: periodStart, periodEnd, notes
- validation: periodEnd > periodStart
- عند الإرسال: `hrApi.payroll.createRun({...})` → 201 + redirect للـ detail

### 3. `app/(authenticated)/hr/payroll/[id]/page.tsx` — Detail
- عرض تفاصيل Run (period, status, totals)
- جدول الـ PayrollItems (لكل موظف: gross, tax, net)
- أزرار: Process / Post (حسب الـ status)
- Modal لتأكيد Process

### 4. `app/(authenticated)/hr/payroll/[id]/payslip/[empId]/page.tsx` — Payslip
- قسيمة راتب موظف واحد
- Basic + Allowances + Deductions + Tax + Social Insurance + Net
- Print-friendly CSS

## Sidebar Navigation (`components/layout/Sidebar.tsx`)

```typescript
const menuItems = [
  { href: '/dashboard', label: 'لوحة التحكم', icon: 'Home' },
  { href: '/finance/accounts', label: 'الحسابات', icon: 'DollarSign' },
  { href: '/inventory/items', label: 'الأصناف', icon: 'Package' },
  { href: '/projects', label: 'المشاريع', icon: 'Briefcase' },
  { href: '/procurement/vendors', label: 'الموردين', icon: 'Truck' },  // Phase 3
  { href: '/procurement/purchase-orders', label: 'أوامر الشراء', icon: 'ShoppingCart' },  // Phase 3
  { href: '/hr/employees', label: 'الموظفين', icon: 'Users' },  // Phase 3.5
  { href: '/hr/attendance', label: 'الحضور', icon: 'CalendarCheck' },  // Phase 3.5
  { href: '/hr/leaves', label: 'الإجازات', icon: 'Calendar' },  // Phase 3.5
  { href: '/hr/payroll', label: 'الرواتب', icon: 'Wallet' },  // Phase 4 🆕
];
```

## لما تشتغل هنا

- إضافة صفحة: `app/<route>/page.tsx` (functional component)
- إضافة API helper: في `lib/api.ts` (grouped: `authApi`, `financeApi`, `inventoryApi`, `projectsApi`, `procurementApi`, `hrApi`)
- Feature جديدة: استخدم `react-hook-form` + `zod` (مُثبَّتان لكن غير مستخدمتين بعد في كل الـ pages)
- Sidebar entry: حدّث `components/layout/Sidebar.tsx` عند إضافة module جديد
- لإضافة shadcn فعلاً: `npx shadcn-ui@latest init` ثم `npx shadcn-ui@latest add button` (يتطلب قرار معماري)

## بعد التعديل

- `npm run type-check` نظيف
- `npm run lint` نظيف
- `npm run build` ينجح
- اختبار التسجيل والدخول من المتصفح
- اختبار الصفحة الجديدة يدوياً (مع auth + role مناسب)
- حدّث الـ Sidebar إذا كانت صفحة جديدة في route group

## مرتبطة بـ

- [`../AGENTS.md`](../AGENTS.md) — root
- [`../backend/AGENTS.md`](../backend/AGENTS.md) — عقود الـ API
- [`../backend/Modules/Identity/AGENTS.md`](../backend/Modules/Identity/AGENTS.md) — Auth flow
- [`../backend/Modules/Procurement/AGENTS.md`](../backend/Modules/Procurement/AGENTS.md) — Phase 3
- [`../backend/Modules/HR/AGENTS.md`](../backend/Modules/HR/AGENTS.md) — Phase 3.5
- [`../backend/Modules/Payroll/AGENTS.md`](../backend/Modules/Payroll/AGENTS.md) — Phase 4
- [`../../AGENTS.md`](../../AGENTS.md) — tech stack الكامل

---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
