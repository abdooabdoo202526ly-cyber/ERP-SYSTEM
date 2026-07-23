# 💻 src/AGENTS.md

> كل الـ source code للمشروع (backend + frontend).
>
> محدّث: 2026-06-24 (Phase 4)

## شو فيه

- `backend/` — كود C# / ASP.NET Core (10 modules: Identity + Finance + Companies + Projects + Inventory + Notifications + Reports + Procurement + HR + Payroll)
- `frontend/` — كود Next.js / TypeScript (24 صفحة عبر 4 phases)

## Phase Modules (Backend)

| Module | Phase | الحالة |
|--------|-------|--------|
| Identity | Phase 0 | ✅ |
| Finance | Phase 1 | ✅ |
| Companies | Phase 1.5 | ✅ |
| Projects | Phase 2.1 | ✅ |
| Inventory | Phase 2.2-2.3 | ✅ |
| EventBus + Outbox | Phase 2.4 | ✅ |
| Reports | Phase 2.5 | ✅ |
| Procurement | Phase 3 | ✅ |
| HR | Phase 3.5 | ✅ |
| **Payroll + EOS** | **Phase 4** | **✅** |
| **AccountsReceivable (AR)** | **Phase 5 Sprint 1** | **✅** |

## Phase Pages (Frontend)

| Phase | عدد الصفحات |
|-------|------------|
| Phase 2.5+ | 8 |
| Phase 3 | 8 (Procurement) |
| Phase 3.5 | 4 (HR) |
| **Phase 4** | **4 (Payroll)** |
| **المجموع** | **24** |

## Conventions

- **Linting** و **formatting** تلقائي على الـ CI — لا تتجاوز warnings
- **No dead code** — لا تترك commented-out code
- **TODOs**: اكتب `// TODO(name): description` لقبولها مؤقتاً، ثم أنشئ issue
- **Backend ↔ Frontend contract**: كل API DTO في Backend له TypeScript interface في Frontend
- **Phase sync**: عند إضافة endpoint جديد، حدّث الـ AGENTS.md الخاص بـ module المعني + `lib/api.ts` + الـ frontend page في نفس الـ PR

## لما تشتغل هنا

- قبل تعديل، اقرأ AGENTS.md للمجلد الفرعي
- حافظ على الـ boundary بين backend و frontend (لا تحطّ logic في الـ frontend)
- اتبع نمط الـ Modular Monolith (Entities / Application / Infrastructure)

## بعد التعديل

- شغّل `dotnet build` و `npm run build` قبل commit
- تأكد أن الـ endpoints الجديدة موثّقة في الـ Swagger
- حدّث `lib/api.ts` (frontend) عند إضافة Backend endpoint
- **حدّث AGENTS.md الخاص بالمجلد اللي تعدّلت فيه** + الـ root `AGENTS.md` Phase Status

## مرتبطة بـ

- [`../AGENTS.md`](../AGENTS.md) — root (Phase Status + Tech Stack)
- [`backend/AGENTS.md`](backend/AGENTS.md) — Backend overview
- [`frontend/AGENTS.md`](frontend/AGENTS.md) — Frontend overview
- [`backend/Modules/*/AGENTS.md`](backend/Modules/) — كل module على حدة
- [`docs/AGENTS.md`](../docs/AGENTS.md) — توثيق المشروع
- [`docs/CHANGELOG.md`](../docs/CHANGELOG.md) — سجل التغييرات


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
