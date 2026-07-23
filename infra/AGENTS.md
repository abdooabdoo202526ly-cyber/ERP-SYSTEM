# 🏗️ infra/AGENTS.md

> البنية التحتية: Docker + CI/CD.
>
> محدّث: 2026-06-24 (Phase 4)

## شو فيه

```
infra/
├── docker/
│   ├── docker-compose.dev.yml     # بيئة التطوير (PostgreSQL 15 + Redis 7)
│   ├── Dockerfile.api             # 🆕 multi-stage build للـ backend
│   └── init-scripts/              # سكربتات Postgres init (multi-DB)
└── .github/
    └── workflows/
        └── ci.yml                 # GitHub Actions (jobs: backend, frontend, docker)
```

> **Phase 4 ملاحظة:** Docker Compose يستخدم PostgreSQL 15 (مُختبَر محلياً بـ 15.18). Redis اختياري (الكود يتعامل مع `AbortOnConnectFail=false`). الـ Health checks تعتمد على `/health/live` و `/health/ready`.

## Conventions

### Docker

- **multi-stage builds** لتقليل حجم الـ images
- **non-root user** في الـ runtime stage
- **HEALTHCHECK** على كل service
- **.dockerignore** (يُضاف عند الحاجة)
- **Secrets** عبر env vars (لا تُحفظ في الـ compose file)

### CI/CD

- **GitHub Actions** فقط
- **PR checks** قبل الـ merge
- **Cache** للـ NuGet و npm
- **Postgres + Redis services** للـ integration tests
- **Docker build** بدون push (للتأكد من الـ Dockerfile)

## لما تشتغل هنا

- إضافة service جديد في `docker-compose.dev.yml`:
  1. حدد الـ image
  2. عرّف env vars
  3. عرّف healthcheck
  4. اربطه بالـ dependencies
- إضافة workflow جديد:
  1. حدد الـ trigger
  2. استخدم نفس الـ env values
  3. cache حيث أمكن

## بعد التعديل

- `docker compose config` يمر بدون errors
- الـ CI يمر على main branch
- Health checks تشتغل في الـ containers

## مرتبطة بـ

- [`../AGENTS.md`](../AGENTS.md)
- [`docker/AGENTS.md`](docker/AGENTS.md)
- [`../src/backend/AGENTS.md`](../src/backend/AGENTS.md)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
