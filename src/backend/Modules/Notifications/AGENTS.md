# 🔔 src/backend/Modules/Notifications/AGENTS.md

> Notifications Module — ✅ Phase 2.3 (in-app notifications).
>
> محدّث: 2026-06-24 — إضافة Phase 3+ context

## شو فيه

```
Notifications/
├── Entities/Notification.cs
├── Infrastructure/NotificationRepository.cs
└── Application/Services/NotificationService.cs
```

## Domain Model

`Notification` (in-DB):
- `Type` (string): "LowStock" حالياً، مستقبلياً "JournalPosted", "HighVariance"...
- `Title`, `Message`
- `ReferenceType` + `ReferenceId` (optional): Item, Project, JournalEntry...
- `IsRead`, `ReadAt`
- `UserId` (target user — حالياً نستخدم creator، مستقبلياً tenant-wide admin)

## Endpoints (3)

| Method | Path | الـ Function |
|--------|------|-------------|
| GET | /api/inventory/notifications | user notifications (paginated) |
| GET | /api/inventory/notifications/unread | unread + count |
| POST | /{id}/mark-read | mark as read |

## لما تشتغل هنا

- إضافة Type جديد: عدّل `NotificationService.CreateAsync` calls
- إضافة channel (email, push): أنشئ `IEmailSender` وادعوه من `NotificationService` (PR #8+)
- تحسين targeting: tenant-wide admin list بدلاً من creator-only

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Inventory/AGENTS.md`](../Inventory/AGENTS.md) — يستدعي LowStock
- [`../Finance/AGENTS.md`](../Finance/AGENTS.md) — JournalPosted alerts (PR #7)
- [`../Procurement/AGENTS.md`](../Procurement/AGENTS.md) — Phase 3 (PO Approved, Bill Due)
- [`../HR/AGENTS.md`](../HR/AGENTS.md) — Phase 3.5 (Leave Approved, Attendance Alert)
- [`../Payroll/AGENTS.md`](../Payroll/AGENTS.md) — Phase 4 (Payroll Processed, Payslip Ready)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
