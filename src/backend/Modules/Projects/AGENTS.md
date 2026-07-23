# 📊 src/backend/Modules/Projects/AGENTS.md

> Projects Module — ✅ Phase 2.1 (مكتمل).
>
> محدّث: 2026-06-24 — إضافة Phase 3+ context

## شو فيه

```
Projects/
├── Entities/
│   ├── Project.cs          # Project + ProjectStatus
│   ├── ProjectTask.cs      # ProjectTask + TaskStatus
│   ├── Resource.cs         # Resource + ResourceType
│   ├── ProjectBudget.cs    # SpentAmount/CommittedAmount/AvailableAmount
│   └── ResourceAssignment.cs # HourlyRate snapshot + computed EstimatedCost
├── Application/
│   ├── ProjectsDtos.cs     # كل الـ DTOs
│   ├── Validators.cs       # FluentValidation
│   └── Services/
│       ├── ProjectService.cs           # CRUD + status workflow + auto-bootstrap
│       └── SupportingServices.cs        # Task, Resource, Budget, Assignment
└── Infrastructure/
    ├── IRepositories.cs
    ├── ProjectRepository.cs
    ├── TaskRepository.cs
    ├── ResourceRepository.cs
    ├── ProjectBudgetRepository.cs      # + RecalculateSpentAsync (SQL agg)
    └── ResourceAssignmentRepository.cs
```

## Domain Model

### Project Lifecycle
```
Planning → Active → OnHold → Completed
    ↓        ↓      ↓
  Cancelled ← Cancelled
```

Forward-only (لا يمكن الرجوع من Completed). Transition invalid → 400 BadRequest.

### ProjectBudget ↔ CostCenter (1:1)
- عند إنشاء Project → CostCenter تلقائياً (type=Project, code=CC-{projectCode})
- ProjectBudget يحمل `cost_center_id` + `account_id` (اختياري)
- **SpentAmount**: يحسب من `journal_lines` (WHERE cost_center_id=... AND je.status=Posted)
  - عبر `RecalculateSpentAsync` — aggregation SQL (debit - credit)
- **AvailableAmount** = BudgetAmount - SpentAmount - CommittedAmount

### ResourceAssignment
- يلتقط `HourlyRate` snapshot وقت التعيين (حتى لو تغير Resource.HourlyRate لاحقاً)
- `EstimatedHours` = (To - From).TotalHours
- `EstimatedCost` = EstimatedHours × HourlyRate

## Endpoints (16)

| Method | Path | الغرض |
|--------|------|-------|
| GET    | /api/projects | قائمة + filter بـ companyId/status |
| GET    | /api/projects/{id} | تفاصيل |
| POST   | /api/projects | إنشاء (auto CostCenter + Budget) |
| PUT    | /api/projects/{id} | تحديث |
| POST   | /api/projects/{id}/status | تغيير الحالة (workflow validation) |
| DELETE | /api/projects/{id} | soft-delete |
| GET    | /api/projects/{id}/tasks | قائمة المهام |
| GET    | /api/projects/{id}/budget | ميزانية |
| POST   | /api/projects/{id}/budget/recalculate | إعادة حساب Spent |
| GET    | /api/projects/{id}/assignments | تعيينات الموارد |
| POST   | /api/projects/{id}/assignments | تعيين مورد |
| DELETE | /api/projects/{id}/assignments/{aid} | إزالة |
| GET/POST/PUT/DELETE | /api/tasks, /api/resources | CRUD |

## لما تشتغل هنا

- إضافة status جديد: عدّل `ProjectStatus` + `ProjectService.ChangeStatusAsync` (الـ validTransitions dict)
- إضافة field: migration جديدة + entity + DTO + service + repo
- حساب Spent: يستدعى `IBudgetService.RecalculateSpentAsync` (الآن يدوي — في Phase 2.4 يُستدعى تلقائياً على PostAsync لـ journal entry)

## بعد التعديل

- شغّل `dotnet test` (20 tests جديد في Phase 2.1)
- إذا غيّرت workflow status: حدّث `ProjectService.ChangeStatusAsync` + tests

## تكامل مع الموديولات الأخرى

- **Finance** (Phase 1): `CostCenter` يُنشأ تلقائياً. `ProjectBudget.SpentAmount` يحسب من `journal_lines` المُرحّلة.
- **Inventory** (Phase 2.2-2.3): `StockMovement.ProjectId` و `ProjectMaterialRequested` event (مستقبلي)
- **Reporting** (Phase 2.5): P&L per Project عبر JOIN على CostCenter

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Identity/AGENTS.md`](../Identity/AGENTS.md)
- [`../Finance/AGENTS.md`](../Finance/AGENTS.md) — cost_center integration
- [`../Companies/AGENTS.md`](../Companies/AGENTS.md) — CostCenter + Company link
- [`../Procurement/AGENTS.md`](../Procurement/AGENTS.md) — Phase 3 (PO/GR per project)
- [`../HR/AGENTS.md`](../HR/AGENTS.md) — Phase 3.5 (Resource Assignment → Employee)
- [`../Payroll/AGENTS.md`](../Payroll/AGENTS.md) — Phase 4 (Resource cost → Salary)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
