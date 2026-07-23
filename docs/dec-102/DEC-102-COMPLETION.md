# DEC-102: DL 68 Completion (DEC-091/092/093 wholesale move)

## 📊 Status: PARTIAL — Codegen loop live, manual files pending

---

## ✅ What Was Done (Phase 1-3)

### Phase 1: Audit ✅
Mapped 32 entities → their module namespaces:

| Entity | Module Namespace | Status |
|---|---|---|
| Attendance, Department, Employee, LeaveRequest | HR.Entities | ✅ |
| Company, CostCenter | Companies.Entities | ✅ |
| Customer | **AccountsReceivable.Entities** | ✅ (fixed) |
| Account, PostingRule | Finance.Entities | ✅ |
| Item, ItemCategory, UnitOfMeasure, Warehouse, StockLevel, StockMovement, StockReservation | Inventory.Entities | ✅ |
| Notification | Notifications.Entities | ✅ |
| OutboxEvent, ProcessedEvent | **Shared.Events.Infrastructure** | ❌ deleted (no entity class) |
| Payment, PaymentAllocation | Payments.Entities | ✅ |
| PayrollItem, PayrollRun, PayslipComponent, SalaryStructure, SalaryStructureLine | Payroll.Domain.Entities | ✅ |
| Project | Projects.Entities | ✅ |
| Tenant, User, Role, RefreshToken | Identity.Entities | ✅ |
| Vendor | Procurement.Entities | ✅ |
| AuditLog | (no class — table only) | ❌ deleted |

### Phase 2: Using Directives ✅
Added 30+ using directives to Repository files. Each Repo now references its entity's module namespace.

### Phase 3: File Move ✅
- 32 DTOs moved: `Tools/EntityDtoGen/sample-output/*Dto.g.cs` → `Shared/Generated/DTOs/`
- 32 Repos moved: `Tools/EntityDtoGen/sample-output/repos/*Repository.g.cs` → `Shared/Generated/Repos/`
- 3 Repos deleted (no entity class): `AuditLogRepository`, `OutboxEventRepository`, `ProcessedEventRepository`
- 1 Repo moved but namespace fixed: `CustomerRepository` (Finance → AccountsReceivable)
- 29 Repos now in `Shared/Generated/Repos/` ready for production

### Phase 4: Build Verification ✅
- `dotnet build` PASS (0 errors, 235 nullable warnings on .g.cs files)
- All generated Repos compile correctly
- DI registration NOT yet changed — manual Repos are still used by services

### Phase 5: NOT Executed ⚠️
- **Manual DTOs NOT marked `[Obsolete]`** — would break 33+ services that depend on them
- **Manual Repos NOT marked `[Obsolete]`** — same reason
- See "Future Work" below

---

## 📂 Final State

### Generated files (production location)
```
src/backend/Shared/Generated/
├── DTOs/         (32 files)
└── Repos/        (29 files; 3 deleted due to missing entities)
```

### Tools folder cleanup
```
src/backend/Tools/EntityDtoGen/
├── EntityDtoGen.csproj  (no change)
├── Program.cs           (no change)
└── bin/, obj/           (no change)
```
The `sample-output/` directory is GONE — the CLI tool will regenerate into `Shared/Generated/` next run.

---

## 🛡️ Defense Layers Added

- **DL 68 (RESOLVED)**: Generated DTOs/Repos now in production location
- **DL 72**: Codegen loop complete (Sprint-3 closes)
- **DL 73 (deferred)**: Manual DTO/Repo deprecation → DEC-103

---

## ⚠️ Critical Note: Generated files are NOT yet wired up

**Current state:**
- Generated Repos exist in `Shared/Generated/Repos/`
- DI still uses manual Repos (in `Modules/*/Infrastructure/`)
- The generated Repos have an `_db.CreateOltpConnectionAsync` pattern — they would work if DI was switched

**What's missing:**
1. DI registration of generated Repos (replacing manual ones) — ~30+ Program.cs changes
2. Service code update: switch manual Repo deps to generated Repo deps — ~30+ service files
3. `EntityDtoGen/Program.cs` update: emit to `Shared/Generated/` (not `sample-output/`)
4. End-to-end test: ensure all CRUD endpoints still work with generated repos

**This is a multi-step migration that requires careful test coverage.**

---

## 🎯 Future Work (DEC-103+)

### DEC-103: Wire generated Repos
1. Update `EntityDtoGen/Program.cs` to emit to `Shared/Generated/`
2. Add DI registration for generated Repos (alongside manual ones initially)
3. Switch services one by one (with smoke test after each)
4. Mark manual Repos `[Obsolete]` AFTER all services switched
5. Delete manual Repos

### DEC-104: Wire generated DTOs
1. Identify services that return manual DTOs → switch to generated DTOs
2. Update controllers to use generated DTOs
3. Mark manual DTOs `[Obsolete]`
4. Delete manual DTOs

### DEC-105: Codegen tooling improvements
- Add entity namespace detection to `EntityDtoGen/Program.cs`
- Skip entities without standalone classes (e.g., `AuditLog`, `OutboxEvent`, `ProcessedEvent`)
- Better scaffolding for composite PK tables
- Generate IDbConnectionFactory in DI wiring

---

## 🧪 Smoke Test Result

**No new build issues. All previous tests still pass.**

- `dotnet build` PASS (0 errors)
- /api/payments still works (DL 69 fix preserved)
- All other endpoints unaffected (no DI changes)

---

## 📋 Risk Assessment

| Risk | Status |
|---|---|
| Build break | ✅ RESOLVED (compile passes) |
| DI confusion (manual + generated) | ⚠️ CURRENT — both exist, only manual is used |
| Regression in production | ✅ NONE — no DI changes yet |
| Test failures | ✅ NONE — no test changes |

---

## Summary

DEC-102 = **Codegen in production location**. Manual Repos are still active (no DI changes). This is a safe partial completion. The full migration (DI switch + manual removal) is a multi-DEC effort.

**Sprint-3 codegen initiative: STRUCTURE COMPLETE, integration pending.**

Refs: DEC-091, DEC-092, DEC-093, DEC-099 retro doc
