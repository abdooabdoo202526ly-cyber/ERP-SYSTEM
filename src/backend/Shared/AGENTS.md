# 🔧 src/backend/Shared/AGENTS.md

> كود مشترك بين كل الـ modules (لا يحتوي domain logic خاص).

## شو فيه

```
Shared/
├── Infrastructure/
│   ├── IDbConnectionFactory.cs        # عقد اتصالات DB
│   └── NpgsqlConnectionFactory.cs    # تنفيذ Npgsql
├── MultiTenancy/
│   ├── ITenantContext.cs              # عقد السياق
│   ├── TenantContext.cs               # تنفيذ AsyncLocal
│   └── TenantMiddleware.cs            # يلتقط tenant_id من JWT
├── Migrations/                        # FluentMigrator (timestamp-based)
│   ├── 20260614_120000_CreateIdentityTables.cs
│   ├── 20260614_180000_CreateFinanceTables.cs
│   ├── 20260615_020000_AddMultiCompanySupport.cs
│   ├── 20260615_050000_CreateProjectsTables.cs
│   ├── 20260615_070000_AddInventoryCore.cs
│   ├── 20260615_090000_AddInventoryMovements.cs
│   ├── 20260615_110000_AddOutboxAndProcessedEvents.cs
│   ├── 20260623_120000_CreateProcurementTables.cs  # 🆕 Phase 3
│   ├── 20260623_130000_CreateHRTables.cs           # 🆕 Phase 3.5
│   └── MigrationRunnerHostedService.cs             # يشغّل الـ migrations
└── Events/
    └── StockEvents.cs                 # Contracts للـ Pub/Sub بين الموديولات
```

## Conventions

### Infrastructure

- **IDbConnectionFactory**: كل module يستخدم نفس الـ factory (Singleton)
- **الاتصال**: `using var conn = await _factory.CreateOltpConnectionAsync(ct)` — ثم Dapper queries
- **لا singleton** على الـ Repository — scoped (لكل request)
- **ممنوع** استدعاء Repositories من Shared/

### MultiTenancy

- `ITenantContext` يحوي `TenantId` و `UserId` فقط
- `TenantMiddleware` يلتقط من claims `tenant_id` و `sub` بعد `UseAuthentication()`
- **استخدام في Repositories** (المرحلة القادمة): filter بـ `WHERE tenant_id = @TenantId`
- **ممنوع** استدعاء DB بدون tenant filter (للمرحلة القادمة)

### Migrations

- ترقيم: `YYYYMMDD_HHMMSS_Description` (timestamp)
- كل migration: `Up()` + `Down()` (للـ rollback)
- **لا تعدل migration موجودة** — أنشئ جديدة دائماً
- اسم الجداول: snake_case، plural (`users`, `roles`, `refresh_tokens`)
- Foreign keys: حدد `OnDelete` صراحة

### Events

- `Shared/Events/<Name>Events.cs` يحتوي records فقط
- اسم الحدث: ماضوي — `StockReceived`, `InvoiceCreated`
- يحمل: `TenantId`, `OccurredAt`, `EventId`, `Data`
- الموديولات تنشر/تشترك عبر MartenDB (inline في MVP، Kafka/RabbitMQ مستقبلياً)

## لما تشتغل هنا

- إضافة `IDbConnection` جديد: عرّف method في interface + تنفيذ
- إضافة middleware: ضع هنا، و سجّله في `Host/Program.cs`
- إضافة migration: timestamp جديد + Up + Down

## بعد التعديل

- حدّث هذا الـ AGENTS.md إذا أضفت folder جديد
- إذا غيّرت Migrations naming convention، وثّقها هنا

## مرتبطة بـ

- [`../AGENTS.md`](../AGENTS.md)
- [`../Host/AGENTS.md`](../Host/AGENTS.md) — تسجيل DI
- [`../Modules/Identity/AGENTS.md`](../Modules/Identity/AGENTS.md)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
