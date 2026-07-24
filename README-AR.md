# 🚀 ERP-SYSTEM - دليل التشغيل السريع (بدون Docker)

## ⚠️ مشكلة Docker

لو عندك مشكلة في Docker (502 Bad Gateway، I/O error، Disk full) — **استخدم Native mode**.

## 🎯 التشغيل في 3 خطوات

### 1. تأكد PostgreSQL 15+ مثبّت
```powershell
# تحميل:
# https://www.postgresql.org/download/windows/

# إضافة psql للـ PATH:
$env:Path += ";C:\Program Files\PostgreSQL\15\bin"
[Environment]::SetEnvironmentVariable("Path", $env:Path, "User")
```

### 2. شخّص الـ setup
```powershell
cd F:\erpsystem7-22-2026\ERP-SYSTEM-v1.0.34-hotfix2
.\scripts\check-pg.ps1
```

### 3. شغّل
```powershell
# طريقة سحرية: يكشف Docker تلقائياً
.\scripts\start.ps1

# أو Native مباشرة (الأفضل لو Docker مكسور):
.\scripts\start-native.ps1
```

## 🌐 الـ URLs

```
Frontend:  http://localhost:3000
Backend:   http://localhost:5000
Swagger:   http://localhost:5000/swagger
Login:     admin@alfajr.local / Demo1234
```

## 📋 كل الأوامر

```powershell
# === التشغيل ===
.\scripts\start.ps1                  # ذكي (Docker أولاً، Native لو مكسور)
.\scripts\start-native.ps1            # Native فقط
.\scripts\start-native.ps1 -Setup     # إنشاء databases
.\scripts\start-native.ps1 -ForceNative   # فرض Native

# === الإدارة ===
.\scripts\start-native.ps1 -Status    # فحص الصحة
.\scripts\start-native.ps1 -Down      # إيقاف
.\scripts\start.ps1 -Status           # فحص شامل

# === التشخيص ===
.\scripts\check-pg.ps1                # PostgreSQL
```

## 🆘 حل المشاكل

| المشكلة | الحل |
|---------|------|
| `psql not found` | `$env:Path += ";C:\Program Files\PostgreSQL\15\bin"` |
| `Port 5432 closed` | `Start-Service postgresql-x64-15` |
| `Port 5000 in use` | `netstat -ano \| findstr :5000` ثم `taskkill /PID X /F` |
| `Port 3000 in use` | `netstat -ano \| findstr :3000` ثم `taskkill /PID X /F` |
| `Docker is broken` | **استخدم `start-native.ps1`** |

## 📂 بنية المشروع

```
ERP-SYSTEM/
├── scripts/
│   ├── start.ps1              # ⭐ الذكي (ابدأ هنا)
│   ├── start-native.ps1       # Native mode
│   ├── check-pg.ps1           # تشخيص PostgreSQL
│   └── quickstart.ps1         # Docker mode (لو يشتغل)
├── src/
│   ├── backend/               # ASP.NET Core 9
│   └── frontend/              # Next.js 14
└── infra/
    └── docker/                # Docker config (للإنتاج)
```

## ✅ متطلبات التشغيل

| البرنامج | الإصدار | التحقق |
|---------|---------|--------|
| .NET SDK | 9.0+ | `dotnet --version` |
| Node.js | 20+ | `node --version` |
| PostgreSQL | 15+ | `psql --version` |
