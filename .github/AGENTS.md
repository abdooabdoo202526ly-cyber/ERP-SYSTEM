# 🔄 .github/AGENTS.md

> GitHub Actions workflows + Repository automation.
>
> محدّث: 2026-06-24 (Phase 4)

## شو فيه

```
.github/
├── workflows/
│   └── ci.yml     # Backend + Frontend + Docker build
└── AGENTS.md      # هذا الملف
```

> **ملاحظة:** الـ workflows انتقلت من `infra/.github/workflows/` إلى `.github/workflows/` ليقرأها GitHub Actions بشكل صحيح.

## Workflows

### ci.yml

- **Triggers**: push / PR على main و develop
- **Concurrency**: يلغي pipelines قديمة على نفس الـ PR
- **Jobs**:
  1. `backend` — restore → build → test مع Postgres + Redis services
  2. `frontend` — install → type-check → lint → build (مع `.eslintrc.json` المثبَّت في Phase 3 لتفادي الـ non-interactive prompt)
  3. `docker` — يبني صورة الـ API (بدون push)

> **Phase 4 ملاحظة:** مع Phase 3 (`feature/phase-3-frontend`) و Phase 4، الـ CI يختبر:
> - backend: 10 migrations (identity → finance → projects → inventory → outbox → procurement → hr → payroll)
> - frontend: 24 صفحة عبر 4 route groups

## Conventions

- **Pinned versions** للـ actions (`@v4`، `@v6`، إلخ)
- **Env vars** مشتركة في `env:` block
- **Cache** للـ NuGet و npm
- **Test results** تُرفع كـ artifacts
- **Secrets** من GitHub Secrets (لا تضعها في الـ YAML)

## PR Rules

- كل push على `main` أو `develop` → CI يـ runs
- كل PR → CI required check قبل الـ merge
- الـ branch protection لازم يكون: "Require status checks to pass before merging"


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
