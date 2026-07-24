# 🚀 ERP-SYSTEM — ابدأ من هنا

## ⚠️ إذا كنت تشغّل على Windows و Docker عندك مكسور

**استخدم هذا الملف فقط:**

```
RUN-ME.bat       ← Double-click
```

أو من PowerShell:
```powershell
.\RUN-ME.ps1
```

**هذا يعمل بدون Docker. كل شيء native.**

---

## 📋 لو Docker يشتغل عندك

```powershell
.\scripts\start.ps1
```

هذا يفحص Docker تلقائياً ويشغّله إذا كان شغّال، أو يتحول للـ native إذا مكسور.

---

## 🆘 مشاكل شائعة

| المشكلة | الحل |
|---------|------|
| `psql not found` | `$env:Path += ";C:\Program Files\PostgreSQL\15\bin"` |
| `Port 5432 closed` | Start PostgreSQL service |
| `Port 5000 in use` | اقتل الـ process |
| `Docker is broken` | استخدم `RUN-ME.bat` (Native) |
| `dotnet not found` | ثبّت .NET 9 من https://dot.net |

---

## 🛠️ كل الأوامر

```powershell
# === الأسهل ===
.\RUN-ME.bat                # أو .\RUN-ME.ps1
.\scripts\start.ps1         # ذكي (Docker أولاً، Native لو مكسور)

# === التشخيص ===
.\scripts\check-pg.ps1      # فحص PostgreSQL
.\scripts\start.ps1 -Status # فحص شامل

# === إيقاف ===
.\scripts\start.ps1 -Down

# === التطوير ===
.\scripts\start-native.ps1   # Native فقط
.\scripts\start-native.ps1 -Setup   # إنشاء databases
```

---

## 📦 ما تحتاج مثبت

| الأداة | الإصدار | الرابط |
|--------|---------|--------|
| .NET SDK | 9+ | https://dot.net |
| Node.js | 20+ | https://nodejs.org |
| PostgreSQL | 15+ | https://www.postgresql.org/download/windows/ |

---

## 🌐 بعد التشغيل

```
Frontend:  http://localhost:3000
Backend:   http://localhost:5000
Swagger:   http://localhost:5000/swagger
Login:     admin@alfajr.local / Demo1234
```
