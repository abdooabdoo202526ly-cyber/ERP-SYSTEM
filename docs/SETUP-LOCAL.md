# 🖥️ دليل الإعداد المحلي (بدون Docker)

> **الحالة:** ✅ مُختبَر محلياً على Windows 11 + PostgreSQL 15.18 + .NET 10 SDK + Node.js 24 (يونيو 2026)
>
> **متى تستخدم هذا الدليل؟**
> - لا يتوفر Docker على جهازك
> - تفضّل تشغيل كل service محلياً للتطوير/debugging
> - تختبر على نفس بيئة المالك (Windows + PostgreSQL installer)
>
> **للإنتاج أو للفريق:** استخدم [`infra/docker/docker-compose.dev.yml`](../infra/docker/docker-compose.dev.yml) عبر `docker compose`.

---

## 📋 المتطلبات الأساسية

| الأداة | الإصدار المطلوب | الإصدار المُختبَر | رابط التحميل |
|--------|-----------------|------------------|------------|
| **PostgreSQL** | 15+ | **15.18** | https://www.postgresql.org/download/windows/ |
| **.NET SDK** | 9.0 (target) أو أعلى | **10.0.101** | https://dotnet.microsoft.com/download/dotnet/9.0 |
| **Node.js** | 18+ (LTS 20 موصى به) | **24.12.0** | https://nodejs.org |
| **Git** | 2.x+ | 2.52.0 | https://git-scm.com |

> **ملاحظة:** .NET 10 SDK يستطيع بناء/تشغيل targets `net9.0` بدون مشاكل.

---

## 🚀 خطوات الإعداد (نفّذها بالترتيب)

### 1) إنشاء PostgreSQL User + Databases

افتح **SQL Shell (psql)** من Start Menu (مع PostgreSQL installer) أو **pgAdmin → Tools → Query Tool**، ثم نفّذ:

```sql
-- 1. إنشاء المستخدم
CREATE USER erp_user WITH PASSWORD 'erp_password';

-- 2. قاعدة البيانات الأولى (OLTP - بيانات العمل)
CREATE DATABASE erp_system OWNER erp_user;

-- 3. قاعدة البيانات الثانية (Event Store)
CREATE DATABASE erp_events OWNER erp_user;

-- 4. الصلاحيات
GRANT ALL PRIVILEGES ON DATABASE erp_system TO erp_user;
GRANT ALL PRIVILEGES ON DATABASE erp_events TO erp_user;
```

**تحقق من النجاح:**

```bash
# من PowerShell (أضف C:\Program Files\PostgreSQL\15\bin للـ PATH إذا لم يكن)
$env:PGPASSWORD = "erp_password"
psql -h localhost -U erp_user -d erp_system -c "SELECT 'OK' as status;"
psql -h localhost -U erp_user -d erp_events -c "SELECT 'OK' as status;"
```

**الناتج المتوقع:**
```
 status
--------
 OK
(1 row)
```

---

### 2) استنساخ المشروع

```powershell
# في PowerShell، انتقل لمجلد العمل
cd C:\Users\<YourName>\projects   # أو أي مكان تختاره

# استنسخ
git clone https://github.com/anas600/ERP-SYSTEM.git
cd ERP-SYSTEM

# تأكد أنك على develop
git branch --show-current    # يجب أن يطبع: develop
```

---

### 3) تشغيل الـ Backend

```powershell
cd src\backend\Host

# استعادة الحزم (مرة واحدة، قد يستغرق 2-5 دقائق في أول مرة)
dotnet restore

# بناء (مرة واحدة)
dotnet build

# تشغيل الـ API
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --no-build
```

**ما يجب أن يحدث عند أول تشغيل:**

```
[INF] FluentMigrator: 20260614_120000: CreateIdentityTables migrated
[INF] FluentMigrator: 20260614_180000: CreateFinanceTables migrated
[INF] FluentMigrator: 20260615_020000: AddMultiCompanySupport migrated
[INF] FluentMigrator: 20260615_050000: CreateProjectsTables migrated
[INF] FluentMigrator: 20260615_070000: AddInventoryCore migrated
[INF] FluentMigrator: 20260615_090000: AddInventoryMovements migrated
[INF] FluentMigrator: 20260615_110000: AddOutboxAndProcessedEvents migrated
[INF] ERPSystem.Shared.Migrations.MigrationRunnerHostedService: تم تنفيذ جميع الـ migrations بنجاح.
[INF] ERPSystem.Shared.Events.Application.Services.OutboxProcessorHostedService: OutboxProcessor started. Polling every 5s
[INF] Microsoft.Hosting.Lifetime: Now listening on: http://localhost:5000
[INF] Microsoft.Hosting.Lifetime: Application started. Press Ctrl+C to shut down.
[INF] Microsoft.Hosting.Lifetime: Hosting environment: Development
```

**تحقق:**
```powershell
# Terminal جديد
curl http://localhost:5000/health
# {"status":"healthy","timestamp":"..."}

curl http://localhost:5000/health/ready
# {"status":"ready","timestamp":"...","checks":{...}}
```

> **مهم:** `OutboxProcessor` يستطلع كل 5 ثوانٍ. في الإنتاج سيُستبدل بـ Postgres LISTEN/NOTIFY (مذكور في الكود).

---

### 4) تشغيل الـ Frontend

**Terminal جديد** (يبقى مفتوحاً):

```powershell
cd src\frontend

# تثبيت الحزم (مرة واحدة)
npm install

# إنشاء ملف .env.local للإشارة إلى الـ Backend المحلي
"NEXT_PUBLIC_API_URL=http://localhost:5000" | Out-File -Encoding utf8 .env.local

# تشغيل dev server
npm run dev
```

**ما يجب أن يحدث:**

```
  ▲ Next.js 14.2.0
  - Local:        http://localhost:3000
  - Environments: .env.local

 ✓ Ready in 4s
```

**تحقق:**
```powershell
# Terminal رابع
curl -s -o /dev/null -w "HTTP %{http_code}\n" http://localhost:3000
# HTTP 200
```

---

### 5) اختبار النظام

افتح المتصفح:

| URL | الوصف |
|---|---|
| http://localhost:3000 | الصفحة الرئيسية |
| http://localhost:3000/register | إنشاء حساب جديد |
| http://localhost:3000/login | تسجيل دخول |
| http://localhost:3000/dashboard | لوحة التحكم (بعد Login) |
| http://localhost:3000/finance/accounts | Chart of Accounts |
| http://localhost:3000/inventory/items | قائمة الأصناف |
| http://localhost:3000/projects | قائمة المشاريع |
| http://localhost:5000/swagger | API Documentation |

---

### 6) إنشاء أول حساب

**عبر الواجهة (الأسهل):**
1. افتح http://localhost:3000/register
2. أدخل:
   - **الاسم الكامل** (مثلاً: `Admin User`)
   - **البريد الإلكتروني** (مثلاً: `admin@company.com`)
   - **كلمة المرور** (8+ أحرف، يجب أن تحتوي على حرف كبير وصغير ورقم — مثلاً: `Pass1234`)
   - **اسم الشركة** (مثلاً: `My Company`)
3. اضغط "تسجيل"
4. سيتم توجيهك لـ `/dashboard` تلقائياً

**ما يحدث في الخلفية:**
- الـ backend ينشئ `Tenant` جديد
- `Subdomain` يُحسب تلقائياً من اسم الشركة عبر `Slugify()` (مثال: `My Company` → `my-company`)
- ينشئ `User` + 4 default roles + يربط `Admin` للمستخدم
- ينشئ `Holding Company` بنفس العملة الافتراضية `LYD`
- يخزّن `accessToken` + `refreshToken` + `user` في `localStorage`

**عبر Terminal (للتأكد):**

```powershell
curl -X POST http://localhost:5000/api/auth/register `
  -H "Content-Type: application/json" `
  -d '{
    "email": "admin@company.com",
    "password": "Pass1234",
    "fullName": "Admin User",
    "tenantName": "My Company"
  }'
```

**الاستجابة المتوقعة:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "...",
  "accessTokenExpiresAt": "2026-06-17T...",
  "refreshTokenExpiresAt": "2026-06-30T...",
  "user": {
    "id": "...",
    "tenantId": "...",
    "email": "admin@company.com",
    "fullName": "Admin User",
    "roles": ["Admin"]
  },
  "holdingCompanyId": "..."
}
```

---

### 7) تسجيل الدخول

1. افتح http://localhost:3000/login
2. أدخل: `admin@company.com` / `Pass1234`
3. اضغط "دخول"
4. ستنتقل لـ `/dashboard`

**تحقق من نجاح Login:**
- في **Network tab** (F12): `POST /api/auth/login` → 200
- في **Application tab → Local Storage**:
  - `accessToken`
  - `refreshToken`
  - `user` (JSON)

---

## 🔍 استكشاف الأخطاء

### ❌ `Failed to connect to 127.0.0.1:5432`

**السبب:** PostgreSQL service لا يعمل.

**الحل (Windows):**
```powershell
# كـ Administrator
net start postgresql-x64-15
```

**تحقق:**
```powershell
Get-Service postgresql-x64-15
# Status: Running
```

---

### ❌ `password authentication failed for user 'erp_user'`

**السبب:** لم تنشئ الـ user بشكل صحيح، أو كلمة السر مختلفة.

**الحل:**
```sql
-- في SQL Shell كـ postgres
DROP USER IF EXISTS erp_user;
CREATE USER erp_user WITH PASSWORD 'erp_password';
GRANT ALL PRIVILEGES ON DATABASE erp_system TO erp_user;
GRANT ALL PRIVILEGES ON DATABASE erp_events TO erp_user;
```

---

### ❌ `relation does not exist` أو migration errors

**السبب:** Migrations لم تُطبَّق.

**الحل:**
1. أوقف الـ Backend (`Ctrl+C`)
2. احذف الـ databases وأنشئها من جديد (الخطوة 1)
3. أعد تشغيل الـ Backend (الـ migrations ستُطبَّق تلقائياً)

---

### ❌ `CORS policy` في الـ Console

**السبب:** الـ Frontend يشير لـ URL خطأ (لا يزال يشير للـ sandbox القديم).

**الحل:**
```powershell
cd src\frontend
# تحقق من .env.local
Get-Content .env.local
# يجب أن يحتوي: NEXT_PUBLIC_API_URL=http://localhost:5000

# امسح cache
Remove-Item -Recurse -Force .next

# أعد التشغيل
npm run dev
```

> **ملاحظة:** إذا ظهر الـ Frontend رابط الـ sandbox القديم (`serveousercontent.com`)، تأكد من وجود `.env.local`. الـ frontend يقرأ `process.env.NEXT_PUBLIC_API_URL` أولاً.

---

### ❌ Port 5000 أو 3000 محجوز

**الـ Backend على port آخر:**
```powershell
dotnet run --no-build --urls=http://localhost:5555
```
ثم حدّث `src\frontend\.env.local`:
```
NEXT_PUBLIC_API_URL=http://localhost:5555
```

**الـ Frontend على port آخر:**
```powershell
npm run dev -- -p 3001
```
ثم افتح http://localhost:3001.

---

### ❌ `psql: command not found` على Windows

**السبب:** PostgreSQL bin غير موجود في PATH.

**حل سريع (للجلسة الحالية):**
```powershell
$env:Path += ";C:\Program Files\PostgreSQL\15\bin"
psql --version    # psql (PostgreSQL) 15.x
```

**حل دائم (يحتاج admin):**
1. Win + R → `sysdm.cpl` → Advanced → Environment Variables
2. في **System variables** → `Path` → Edit
3. New: `C:\Program Files\PostgreSQL\15\bin`
4. OK → افتح PowerShell جديد

---

## 📊 Health Endpoints Reference

| Endpoint | الغرض | يفحص |
|---------|-------|------|
| `GET /health` | Liveness (خفيف) | خدمة الـ API فقط |
| `GET /health/live` | Liveness (في HealthController) | نفس `/health` لكن عبر controller مستقل |
| `GET /health/ready` | Readiness | Postgres + Redis (Redis اختياري) |

**مثال output من `/health/ready`:**
```json
{
  "status": "ready",
  "timestamp": "2026-06-16T22:45:31Z",
  "checks": {
    "postgres_oltp": {
      "healthy": true,
      "latencyMs": 12,
      "version": "PostgreSQL 15.18"
    },
    "redis": {
      "healthy": false,
      "error": "not configured"
    }
  }
}
```

> **ملاحظة:** Redis غير مطلوب. لو ظهر `"redis": { "healthy": false }`، هذا طبيعي في dev بدون Redis.

---

## 🛑 إيقاف النظام (بترتيب عكسي)

1. **Frontend:** `Ctrl+C` في Terminal الـ Frontend
2. **Backend:** `Ctrl+C` في Terminal الـ Backend
3. **PostgreSQL:** (اختياري - يمكنك إبقاؤه يعمل)
   ```powershell
   net stop postgresql-x64-15
   ```

---

## 🔄 إعادة تشغيل بعد إغلاق

```powershell
# 1) PostgreSQL (إذا أوقفته)
net start postgresql-x64-15

# 2) Backend
cd src\backend\Host
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --no-build

# 3) Frontend (terminal جديد)
cd src\frontend
npm run dev

# 4) افتح http://localhost:3000
```

---

## 🎯 Auth API Contracts (مرجع سريع)

### Register

```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",      // required, email
  "password": "Pass1234",            // required, ≥8 chars, [A-Z], [a-z], [0-9]
  "fullName": "John Doe",            // required, ≤200 chars
  "tenantName": "Acme Corp",         // required (لإنشاء tenant جديد)
  "baseCurrency": "LYD"              // optional, default "LYD"
}
```

**Logic:**
- إذا `tenantName` موجود → ينشئ `Tenant` جديد + 4 default roles + يربط `Admin` للمستخدم
- `Subdomain` يُحسب تلقائياً: `Slugify("Acme Corp")` = `"acme-corp"`

### Login

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Pass1234",
  "tenantId": "optional-guid"        // optional، بحث شامل إن لم يُرسل
}
```

### Response (register & login)

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "...",
  "accessTokenExpiresAt": "2026-06-17T...",
  "refreshTokenExpiresAt": "2026-06-30T...",
  "user": {
    "id": "...",
    "tenantId": "...",
    "email": "...",
    "fullName": "...",
    "roles": ["Admin"]
  },
  "holdingCompanyId": "..."
}
```

### Authenticated Endpoints

| Method | Endpoint | الوصف |
|--------|----------|-------|
| `GET`  | `/api/auth/me` | معلومات المستخدم الحالي |
| `POST` | `/api/auth/refresh` | تجديد Access Token (Token rotation) |
| `POST` | `/api/auth/logout` | إلغاء Refresh Token |
| `GET`  | `/api/finance/accounts` | Chart of Accounts |
| `GET`  | `/api/inventory/items` | قائمة الأصناف |
| `GET`  | `/api/projects` | قائمة المشاريع |
| `GET`  | `/api/reports/finance/trial-balance?asOfDate=2026-06-16` | ميزان المراجعة |

كل الـ endpoints المحمية تحتاج: `Authorization: Bearer <accessToken>`

---

## ❓ أسئلة شائعة

**Q: ليش ما أستخدم Docker؟**
A: Docker أسرع للفريق الموحّد (كل المطورين نفس البيئة)، لكن للتطوير الفردي على Windows بدون Docker، هذا الدليل يعمل. راجع [`infra/docker/docker-compose.dev.yml`](../infra/docker/docker-compose.dev.yml) للمقارنة.

**Q: ليش PostgreSQL 15 بدل 16؟**
A: PLAN.md v2.0 يوضح: "متوفر أكثر، API مستقر". كلا الإصدارين متوافقان مع الكود. الإصدار المثبت محلياً في يونيو 2026 كان 15.18 (متوفر مع PostgreSQL installer الرسمي).

**Q: ليش Redis اختياري؟**
A: الـ backend يتفحص `ConnectionStrings:Redis` في `appsettings.json`. لو فاضي أو Redis غير مثبت، الـ backend يستمر بدونه. مطلوب فقط للـ cache المتقدم في الإنتاج.

**Q: وين shadcn/ui؟**
A: `package.json` يحوي `shadcn-ui@0.8.0` كـ **CLI generator** (لتوليد components)، لكن لم يُشغَّل `shadcn-ui init` بعد. كل الـ UI مكتوب بـ Tailwind CSS مباشرة. راجع [`src/frontend/AGENTS.md`](../src/frontend/AGENTS.md) للتفاصيل.

**Q: كيف أوقف الـ OutboxProcessor؟**
A: في `Program.cs`:
```csharp
builder.Services.AddHostedService<OutboxProcessorHostedService>();
```
علّق على هذا السطر. الـ OutboxProcessor يستطلع `outbox_events` الجدول كل 5 ثوانٍ ويرسل events.

---

## 📚 مراجع

- [`README.md`](../README.md) — نظرة عامة
- [`docs/PLAN.md`](PLAN.md) — خطة المشروع (8-10 أسابيع)
- [`AGENTS.md`](../AGENTS.md) — توثيق الـ AI agents (root)
- [`src/backend/AGENTS.md`](../src/backend/AGENTS.md) — Backend conventions
- [`src/frontend/AGENTS.md`](../src/frontend/AGENTS.md) — Frontend conventions
- [`infra/docker/AGENTS.md`](../infra/docker/AGENTS.md) — Docker setup
- [`docs/CHANGELOG.md`](CHANGELOG.md) — سجل التغييرات
