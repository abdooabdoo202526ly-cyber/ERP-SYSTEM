# 🐳 infra/docker/AGENTS.md

> Docker Compose للتطوير + init scripts.
>
> محدّث: 2026-06-24 — إضافة Phase 4 context

## شو فيه

```
docker/
├── docker-compose.dev.yml
└── init-scripts/
    └── 01-create-multiple-databases.sh
```

## Services

| Service | Image | Port | الغرض |
|---------|-------|------|--------|
| `postgres` | **postgres:15-alpine** | 5432 | OLTP + EventStore (قاعدتين) |
| `redis` | redis:7-alpine | 6379 | Cache + Session (اختياري في dev) — Phase 4: timeout 500ms cap |
| `api` | (build from Dockerfile) | 5000 | الـ Backend API |
| `frontend` | node:20-alpine | 3000 | Next.js dev server |

> **Phase 4 ملاحظة:** Redis اختُصر timeout إلى 500ms في `Program.cs` (ConnectTimeout=1s, SyncTimeout=500ms). Health check في `HealthController.cs` يـ cap على 500ms عبر CTS. نتيجة: `/health/ready` من 5000ms+ → 608ms.

> **ملاحظة:** AGENTS السابقة ذكرت `postgres:16-alpine`، لكن PLAN.md v2.0 والـ root AGENTS.md يعتمدان **PostgreSQL 15** (متوفر أكثر، API مستقر). تم توحيد الإصدار إلى 15 هنا و في `docker-compose.dev.yml`.

## Conventions

- **Service naming**: `erp-<name>` للـ containers
- **Networks**: default network
- **Volumes** للـ data persistence + node_modules (تجنب re-install)
- **Init scripts** مرقّمة: `01-...`, `02-...` (ترتيب التنفيذ)
- **Healthchecks** على كل service يعتمد عليه آخر

## init-scripts/

- تُشغّل مرة واحدة عند إنشاء الـ volume لأول مرة
- `01-create-multiple-databases.sh`: يقرأ المتغير `POSTGRES_MULTIPLE_DATABASES` (مثلاً `"erp_system:erp_events"`) وينشئ كل قاعدة + يمنح الصلاحيات لـ `POSTGRES_USER`
- في `docker-compose.dev.yml`: `POSTGRES_MULTIPLE_DATABASES: "erp_system:erp_events"`
- **مهم:** إذا غيّرت الـ databases، يجب حذف الـ volume (`docker volume rm <project>_postgres_data`) لإعادة التهيئة

## لما تشتغل هنا

- إضافة service: تأكد من الـ healthcheck
- تغيير connection strings: انتبه للفرق بين `localhost` (محلي) و service names (داخل Docker)
- **JWT secret** في الإنتاج: استخدم Docker secrets أو env من CI

## بعد التعديل

- `docker compose -f infra/docker/docker-compose.dev.yml config` نظيف
- الـ containers تبدأ بنجاح
- الـ migrations تشتغل تلقائياً

## مرتبطة بـ

- [`../AGENTS.md`](../AGENTS.md)
- [`../../src/backend/AGENTS.md`](../../src/backend/AGENTS.md)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
