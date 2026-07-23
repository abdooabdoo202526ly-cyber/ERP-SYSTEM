# 🚀 START FROM ZERO — v1.0.15

> **3 خطوات فقط. لو اتبعت بالترتيب، هيشتغل 100%.**

---

## ✅ الخطوة 1: Extract

فكّ ضغط `ERP-SYSTEM-v1.0.15.zip` في فولدر جديد:
```
F:\erpsystem7-23-2026\ERP-SYSTEM\
```

⚠️ **مهم:** الفولدر يكون جديد. مش فوق v1.0.8 أو v1.0.13. لو الـ ZIP يسألك على overwrite، اختار **No to All**.

---

## ✅ الخطوة 2: Install (3-4 دقائق)

افتح **PowerShell كـ Administrator** (مهم) وشغّل:

```powershell
cd F:\erpsystem7-23-2026\ERP-SYSTEM
.\scripts\INSTALL-ULTIMATE.ps1 -Reset
```

الـ script هيـ:
1. ✅ يوقف containers القديمة
2. ✅ يحذف images القديمة
3. ✅ **يمسح الـ postgres volume** (اللي فيه الـ schema الناقص)
4. ✅ يبني images جديدة
5. ✅ يشغّل containers
6. ✅ يستنى services
7. ✅ يشغّل SchemaMigrator
8. ✅ **يتحقق من الـ 51 table** في الـ DB
9. ✅ يفتح المتصفح

---

## ✅ الخطوة 3: Login

افتح http://localhost:3000 وسجّل دخول:

| | |
|---|---|
| **Email** | `admin@alfajr.local` |
| **Password** | `Demo1234` |

لو فتح الـ dashboard بدون errors → **تم بنجاح!**

---

## 🧪 لو عايز تتأكد 100% (5 دقائق)

شغّل الـ Playwright tests:

```powershell
cd F:\erpsystem7-23-2026\ERP-SYSTEM\scripts\playwright
npm install
npx playwright install --with-deps chromium
npx playwright test
```

**المتوقع:** 60 passed, 0 failed.

---

## 🆘 لو في Error (Emergency)

```powershell
# 1. شوف اللوق
docker logs erp-api --tail 100

# 2. لو الـ schema فيه مشكلة، شغّل الـ emergency fix
.\scripts\EMERGENCY-RESET-DB.ps1

# 3. Restart API
docker compose -p erp-system -f infra\docker\docker-compose.dev.yml restart api

# 4. لو لسه في مشكلة، ابعتي اللوق
```

---

## 🐛 المشاكل الشائعة وحلولها

| المشكلة | السبب | الحل |
|---------|-------|------|
| `relation "X" does not exist` | Volume قديم | `.\scripts\INSTALL-ULTIMATE.ps1 -Reset` |
| `company_id violates not-null` | Schema قديم | `.\scripts\EMERGENCY-RESET-DB.ps1` |
| Login fails "401" | Tenant أو user مش موجودين | `docker exec erp-postgres psql -U erp_user -d erp_system -c "SELECT email FROM users;"` |
| `Hydration mismatch` warning | Browser cache | `Ctrl+Shift+R` (hard reload) |
| Frontend 404 | Image قديم | `docker rmi erp-system-frontend:latest` ثم `INSTALL-ULTIMATE.ps1` |

---

## 📋 التحقق النهائي (manual)

```powershell
# 1. الـ 4 containers شغّالين؟
docker compose -p erp-system -f infra\docker\docker-compose.dev.yml ps
# المتوقع: erp-api (healthy), erp-postgres (healthy), erp-redis (healthy), erp-frontend (Up)

# 2. الـ tables موجودة؟
docker exec erp-postgres psql -U erp_user -d erp_system -c "\dt" | Measure-Object -Line
# المتوقع: 51+ tables (اللي نعرفها + system tables)

# 3. الـ critical tables موجودة؟
docker exec erp-postgres psql -U erp_user -d erp_system -c "SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename IN ('tenants','users','accounts','customers','vendors','items','purchase_orders','sales_invoices','audit_log');"
# المتوقع: 9 rows

# 4. Login بيشتغل؟
$body = '{"email":"admin@alfajr.local","password":"Demo1234"}'
Invoke-RestMethod -Uri http://localhost:5000/api/auth/login -Method Post -Body $body -ContentType "application/json"
# المتوقع: accessToken في الـ response
```

---

## 🎯 لو كله تمام

افتح http://localhost:3000 في المتصفح واستمتع بالنظام! 🎉

- **Dashboard**: http://localhost:3000/dashboard
- **Chart of Accounts**: http://localhost:3000/finance/accounts
- **Customers**: http://localhost:3000/finance/customers
- **Vendors**: http://localhost:3000/procurement/vendors
- **Items**: http://localhost:3000/inventory/items
- **Employees**: http://localhost:3000/hr/employees
- **Swagger**: http://localhost:5000/swagger

---

## 📞 في حالة المشاكل

ابعتلي:
1. نتيجة: `docker logs erp-api --tail 50 | Select-String "ERR"`
2. نتيجة: `npx playwright test` (لو عملت tests)
3. سكرين شوت من الـ browser لو في error

---

**🎉 بالتوفيق! لو اشتغل، النظام جاهز للـ client demo.**
